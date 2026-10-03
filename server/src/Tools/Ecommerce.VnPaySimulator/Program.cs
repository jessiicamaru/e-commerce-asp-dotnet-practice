using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

// VNPay's payment page, simulated (specs/143). Development, CI and Bruno pay through it the way a customer pays at VNPay:
//   GET  /paymentv2/vpcpay.html?vnp_...   checks the link exactly as the gateway would, and shows Pay and Cancel
//   POST /paymentv2/complete              tells the shop by a signed IPN, server to server, then sends the browser back
// Never part of production: the production overlay keeps it behind a profile nobody turns on there.

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5064");
}

builder.Services.AddHttpClient();

var app = builder.Build();

var tmnCode = Environment.GetEnvironmentVariable("VNPAY_TMN_CODE") ?? "";
var hashSecret = Environment.GetEnvironmentVariable("VNPAY_HASH_SECRET") ?? "";
var ipnUrl = Environment.GetEnvironmentVariable("VNPAY_SIMULATOR_IPN_URL") ?? "http://localhost:5000/api/payments/vnpay/ipn";

if (tmnCode.Length == 0 || hashSecret.Length == 0)
{
    throw new InvalidOperationException("VNPAY_TMN_CODE and VNPAY_HASH_SECRET are required - the same values Payment signs with.");
}

app.Logger.LogWarning("VNPay SIMULATOR for merchant {TmnCode}: no money is moved. Notifications go to {IpnUrl}.", tmnCode, ipnUrl);

app.MapGet("/health", () => Results.Json(new { status = "Healthy", service = "VNPay simulator", ipnUrl }));

app.MapGet("/paymentv2/vpcpay.html", (HttpRequest request) =>
{
    var query = request.Query.ToDictionary(p => p.Key, p => p.Value.ToString());
    var problem = Check(query);
    if (problem is not null)
    {
        return Page("Giao dịch không hợp lệ - Invalid transaction", $"<p class=\"error\">{Html(problem)}</p>", 400);
    }

    var amount = long.Parse(query["vnp_Amount"], CultureInfo.InvariantCulture) / 100m;
    var hidden = string.Concat(query.Select(p => $"<input type=\"hidden\" name=\"{Html(p.Key)}\" value=\"{Html(p.Value)}\">"));
    return Page("VNPay - simulator", $"""
        <p class="note">Simulator: no money is moved.</p>
        <dl>
          <dt>Merchant</dt><dd>{Html(query["vnp_TmnCode"])}</dd>
          <dt>Order</dt><dd>{Html(query.GetValueOrDefault("vnp_OrderInfo", ""))}</dd>
          <dt>Amount</dt><dd id="amount">{amount.ToString("#,0", CultureInfo.InvariantCulture)} VND</dd>
        </dl>
        <form method="post" action="/paymentv2/complete">
          {hidden}
          <button type="submit" name="choice" value="pay">Pay</button>
          <button type="submit" name="choice" value="cancel" class="secondary">Cancel</button>
        </form>
        """);
});

app.MapPost("/paymentv2/complete", async (HttpRequest request, IHttpClientFactory http, ILogger<Program> logger) =>
{
    var form = await request.ReadFormAsync();
    var link = form.Where(p => p.Key.StartsWith("vnp_", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value.ToString());

    // The link again, as submitted: a form edited on its way back is refused like any forged link.
    var problem = Check(link);
    if (problem is not null)
    {
        return Page("Giao dịch không hợp lệ - Invalid transaction", $"<p class=\"error\">{Html(problem)}</p>", 400);
    }

    var paid = form["choice"] == "pay";
    var now = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    var transactionNo = paid ? Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture) : "0";
    var answer = new Dictionary<string, string>
    {
        ["vnp_Amount"] = link["vnp_Amount"],
        ["vnp_BankCode"] = paid ? "NCB" : "VNPAY",
        ["vnp_BankTranNo"] = paid ? $"VNP{transactionNo}" : "",
        ["vnp_CardType"] = paid ? "ATM" : "",
        ["vnp_OrderInfo"] = link.GetValueOrDefault("vnp_OrderInfo", ""),
        ["vnp_PayDate"] = now,
        // 00 paid; 24 the customer cancelled - VNPay's own codes.
        ["vnp_ResponseCode"] = paid ? "00" : "24",
        ["vnp_TmnCode"] = link["vnp_TmnCode"],
        ["vnp_TransactionNo"] = transactionNo,
        ["vnp_TransactionStatus"] = paid ? "00" : "02",
        ["vnp_TxnRef"] = link["vnp_TxnRef"],
    };
    var signed = Encoded(answer) + "&vnp_SecureHash=" + Sign(Encoded(answer));

    // Server to server first, as VNPay does: the shop learns the outcome from this call, never from the browser.
    try
    {
        var response = await http.CreateClient().GetStringAsync($"{ipnUrl}?{signed}");
        logger.LogInformation("IPN for {TxnRef} ({Outcome}) answered {Answer}", answer["vnp_TxnRef"], paid ? "paid" : "cancelled", response);
    }
    catch (Exception exception)
    {
        // VNPay would retry; the simulator says so and still sends the customer back.
        logger.LogWarning(exception, "IPN for {TxnRef} could not be delivered to {IpnUrl}", answer["vnp_TxnRef"], ipnUrl);
    }

    var returnUrl = link["vnp_ReturnUrl"];
    return Results.Redirect($"{returnUrl}{(returnUrl.Contains('?') ? '&' : '?')}{signed}");
});

app.Run();

// What the real gateway checks before showing its page.
string? Check(IReadOnlyDictionary<string, string> query)
{
    if (!query.TryGetValue("vnp_SecureHash", out var hash) || hash.Length == 0)
        return "The link is not signed (vnp_SecureHash).";
    if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(Encoded(query))), Encoding.ASCII.GetBytes(hash.ToLowerInvariant())))
        return "The signature does not match (code 70).";
    if (query.GetValueOrDefault("vnp_TmnCode") != tmnCode)
        return "Unknown merchant (code 02).";
    if (query.GetValueOrDefault("vnp_Version") != "2.1.0" || query.GetValueOrDefault("vnp_Command") != "pay")
        return "Only vnp_Version 2.1.0 and vnp_Command pay are accepted.";
    if (query.GetValueOrDefault("vnp_CurrCode") != "VND")
        return "VNPay settles in VND only.";
    if (!long.TryParse(query.GetValueOrDefault("vnp_Amount"), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        return "vnp_Amount must be a positive whole number of hundredths of a dong.";
    if (!Uri.TryCreate(query.GetValueOrDefault("vnp_ReturnUrl"), UriKind.Absolute, out _))
        return "vnp_ReturnUrl must be an absolute address.";
    if (string.IsNullOrEmpty(query.GetValueOrDefault("vnp_TxnRef")))
        return "vnp_TxnRef is missing.";
    if (!DateTime.TryParseExact(query.GetValueOrDefault("vnp_ExpireDate"), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expires)
        || expires < DateTime.UtcNow.AddHours(7))
        return "The link has expired (code 11).";
    return null;
}

// VNPay 2.1.0: every non-empty vnp_ parameter but the hash, sorted by key (ordinal), URL-encoded, joined with &.
static string Encoded(IEnumerable<KeyValuePair<string, string>> parameters)
{
    var builder = new StringBuilder();
    foreach (var (key, value) in parameters
                 .Where(p => p.Key.StartsWith("vnp_", StringComparison.Ordinal) && p.Key != "vnp_SecureHash" && p.Key != "vnp_SecureHashType" && p.Value.Length > 0)
                 .OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        if (builder.Length > 0) builder.Append('&');
        builder.Append(WebUtility.UrlEncode(key)).Append('=').Append(WebUtility.UrlEncode(value));
    }
    return builder.ToString();
}

string Sign(string data)
{
    using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hashSecret));
    return string.Concat(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
}

static string Html(string value) => WebUtility.HtmlEncode(value);

static IResult Page(string title, string body, int status = 200) => Results.Content($$"""
    <!doctype html>
    <html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <title>{{Html(title)}}</title>
    <style>
      body { font-family: system-ui, sans-serif; max-width: 28rem; margin: 3rem auto; padding: 0 1rem; color: #1f2937; }
      h1 { font-size: 1.25rem; color: #005baa; } .note { background: #fef3c7; padding: .5rem .75rem; border-radius: .375rem; }
      dl { display: grid; grid-template-columns: auto 1fr; gap: .25rem 1rem; } dt { color: #6b7280; } dd { margin: 0; font-weight: 600; }
      button { font-size: 1rem; padding: .6rem 1.4rem; border: 0; border-radius: .375rem; background: #005baa; color: white; margin-right: .5rem; cursor: pointer; }
      button.secondary { background: #e5e7eb; color: #1f2937; } .error { color: #b91c1c; }
    </style></head>
    <body><h1>{{Html(title)}}</h1>{{body}}</body></html>
    """, "text/html; charset=utf-8", statusCode: status);

// The same fall-back-never-override .env loader as every service, so `dotnet run` reads server/.env.
static void LoadDotEnv()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        var path = Path.Combine(directory.FullName, ".env");
        if (!File.Exists(path)) continue;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2 && Environment.GetEnvironmentVariable(parts[0].Trim()) is null)
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
        return;
    }
}
