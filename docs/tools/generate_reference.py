"""Generates the reference pages under docs/reference/ from the code, so they cannot drift from it.

    python docs/tools/generate_reference.py          # from the repository root

Writes five pages:
    api.md          every HTTP endpoint, per service, with who may call it
    messages.md     every integration message, who publishes it and who consumes it
    grpc.md         every gRPC service, who serves it and who calls it
    data-model.md   every table per service database, with its columns (from the EF model snapshots)
    gateway.md      every gateway route and the cluster it reaches

It READS source files and never runs them: attributes, records and snapshots are parsed with regular
expressions tuned to this codebase's style. When a page looks wrong, fix the parser rather than the page -
a hand edit is overwritten on the next run. Run it in the same change as anything that adds an endpoint,
a message, a table or a route; the docs README says so too.
"""
import json
import os
import re
import subprocess
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SERVER = os.path.join(ROOT, "server", "src")
OUT = os.path.join(ROOT, "docs", "reference")

SERVICES = ["Identity", "Catalog", "Cart", "Order", "Inventory", "Payment", "Orchestrator", "Activity"]

ROLE_CONSTANTS = {
    "StaffRoles.Staff": "Admin, Moderator",
    "StaffRoles.Admin": "Admin",
    "StaffRoles.Moderator": "Moderator",
    "RoleNames.Admin": "Admin",
    "RoleNames.Seller": "Seller",
    "RoleNames.Customer": "Customer",
    "RoleNames.Moderator": "Moderator",
}


def read(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read()


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def files(base, suffix):
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for name in filenames:
            if name.endswith(suffix):
                yield os.path.join(dirpath, name)


def service_of(path):
    match = re.search(r"Services[\\/](\w+)[\\/]", path)
    if match:
        return match.group(1)
    if "ApiGateway" in path:
        return "Gateway"
    return "Shared"


def header(title, intro):
    commit = subprocess.run(["git", "rev-parse", "--short", "HEAD"], capture_output=True, text=True, cwd=ROOT).stdout.strip()
    return (f"# {title}\n\n> **Generated** by [`docs/tools/generate_reference.py`](../tools/generate_reference.py) "
            f"from commit `{commit}`. Do not edit by hand - change the code and run the script again.\n\n{intro}\n\n")


def write(name, text):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(text.rstrip() + "\n")
    print(f"wrote docs/reference/{name}")


# ------------------------------------------------------------------------------------------ endpoints

ATTRIBUTE = re.compile(r"^\s*\[(.+)\]\s*$")
METHOD = re.compile(r"^\s*public\s+(?:async\s+)?[\w<>\[\],\s?]+\s+(\w+)\s*\(")
CLASS = re.compile(r"^\s*public\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)")


def roles_in(attribute):
    """'Authorize(Roles = StaffRoles.Staff)' -> 'Admin, Moderator'; 'Authorize' -> 'signed in'."""
    if attribute.startswith("AllowAnonymous"):
        return "anyone"
    match = re.search(r"Roles\s*=\s*([^)]+)", attribute)
    if not match:
        return "signed in"
    value = match.group(1).strip()
    if value.startswith('"'):
        return ", ".join(r.strip() for r in value.strip('"').split(","))
    return ROLE_CONSTANTS.get(value, value)


def http_of(attribute):
    match = re.match(r"Http(Get|Post|Put|Delete|Patch)(?:\(\s*\"([^\"]*)\"\s*\))?", attribute)
    return (match.group(1).upper(), match.group(2) or "") if match else None


def clean_route(route):
    return re.sub(r"\{(\w+)(?::[^}]*)?\}", r"{\1}", route)


def summary_from(doc_lines):
    text = " ".join(line.strip().lstrip("/").strip() for line in doc_lines)
    match = re.search(r"<summary>(.*?)</summary>", text)
    if not match:
        return ""
    summary = re.sub(r"<[^>]+>", "", match.group(1))
    summary = re.sub(r"\s+", " ", summary).strip()
    return summary.replace("|", "\\|")


def endpoints():
    found = []
    for path in files(SERVER, "Controller.cs"):
        if "Controllers" not in path or "ApiControllerBase" in path:
            continue
        service = service_of(path)
        class_route, class_who, pending, docs = "api/[controller]", None, [], []
        controller = None
        for line in read(path).splitlines():
            stripped = line.strip()
            if stripped.startswith("///"):
                docs.append(stripped)
                continue
            attribute = ATTRIBUTE.match(line)
            if attribute:
                pending.append(attribute.group(1))
                continue
            klass = CLASS.match(line)
            if klass:
                controller = klass.group(1)
                for a in pending:
                    if a.startswith("Route("):
                        class_route = re.search(r"\"([^\"]*)\"", a).group(1)
                    if a.startswith("Authorize") or a.startswith("AllowAnonymous"):
                        class_who = roles_in(a)
                pending, docs = [], []
                continue
            method = METHOD.match(line)
            if method and controller:
                http = next((http_of(a) for a in pending if http_of(a)), None)
                if http:
                    verb, template = http
                    who = next((roles_in(a) for a in pending if a.startswith(("Authorize", "AllowAnonymous"))), None)
                    base = class_route.replace("[controller]", controller.removesuffix("Controller").lower())
                    route = template if template.startswith("/") else f"{base}/{template}" if template else base
                    found.append({
                        "service": service, "verb": verb, "path": "/" + clean_route(route).strip("/"),
                        "who": who or class_who or "anyone", "summary": summary_from(docs),
                        "controller": controller, "file": rel(path),
                    })
                pending, docs = [], []
            elif stripped and not stripped.startswith(("{", "}", "//")):
                if not line.lstrip().startswith("["):
                    docs = [] if not method else docs
    return found


def api_page():
    rows = endpoints()
    by_service = defaultdict(list)
    for e in rows:
        by_service[e["service"]].append(e)
    text = header("HTTP API", (
        "Every endpoint a service exposes, grouped by service. Paths are the service's own; the gateway "
        "forwards `/api/...` to them unchanged (see [gateway.md](gateway.md)). **Who** is what the "
        "controller attributes allow - the handler may refuse further (somebody else's product is a 404, "
        "not a 403, for example); the feature documents say where.\n\n"
        f"**{len(rows)} endpoints** across {len(by_service)} services."))
    for service in SERVICES + ["Gateway"]:
        items = sorted(by_service.get(service, []), key=lambda e: (e["path"], e["verb"]))
        if not items:
            continue
        text += f"## {service} ({len(items)})\n\n| Method | Path | Who | What |\n| :-- | :-- | :-- | :-- |\n"
        for e in items:
            text += f"| `{e['verb']}` | `{e['path']}` | {e['who']} | {e['summary']} |\n"
        text += "\n"
    write("api.md", text)
    return len(rows)


# ------------------------------------------------------------------------------------------ messages

def messages_page():
    contracts = os.path.join(SERVER, "BuildingBlocks", "Ecommerce.Contracts")
    records = {}
    for path in files(contracts, ".cs"):
        domain = os.path.basename(os.path.dirname(path))
        for name in re.findall(r"public\s+record\s+(\w+)\s*\(", read(path)):
            records[name] = domain
    # Also the saga's own events, which live beside the state machine.
    publishers, consumers = defaultdict(set), defaultdict(set)
    for path in files(os.path.join(SERVER, "Services"), ".cs"):
        if "Migrations" in path:
            continue
        source = read(path)
        service = service_of(path)
        for name in records:
            if re.search(rf"new\s+(?:[\w.]+\.)?{name}\s*\(", source) and f"IConsumer<{name}>" not in source:
                publishers[name].add(service)
            for consumer in re.findall(rf"^\s*public\s+(?:sealed\s+)?class\s+(\w+)[^{{]*IConsumer<(?:[\w.]+\.)?{name}>", source, re.M):
                consumers[name].add(f"{service} (`{consumer}`)")
            if re.search(rf"Event<(?:[\w.]+\.)?{name}>", source):
                consumers[name].add(f"{service} (saga)")
    # The audit trail and the notifier publish on behalf of whichever service calls them.
    shared = os.path.join(SERVER, "BuildingBlocks", "Ecommerce.Shared")
    for path in files(shared, ".cs"):
        for name in records:
            if re.search(rf"new\s+(?:[\w.]+\.)?{name}\s*\(", read(path)):
                publishers[name].add("any service, through `Ecommerce.Shared`")
    # A record used only inside another message (a line of an order) is part of that message, not one.
    records = {n: d for n, d in records.items() if not n.endswith("Dto")}
    text = header("Messages", (
        "Every integration message in `Ecommerce.Contracts` - the only coupling between services - with "
        "who publishes it and who consumes it. Every publish goes through the publisher's transactional "
        "outbox, and every consumer is idempotent (see "
        "[reliable messaging](../architecture/reliable-messaging-and-outbox-pattern.md)). A consumer's "
        "class name is its queue name, so two services never share one.\n\n"
        f"**{len(records)} messages.**"))
    text += "| Message | Owner | Published by | Consumed by |\n| :-- | :-- | :-- | :-- |\n"
    for name in sorted(records, key=lambda n: (records[n], n)):
        text += (f"| `{name}` | {records[name]} | {', '.join(sorted(publishers[name])) or '-'} | "
                 f"{', '.join(sorted(consumers[name])) or '-'} |\n")
    write("messages.md", text)
    return len(records)


# ------------------------------------------------------------------------------------------ gRPC

def grpc_page():
    protos = os.path.join(SERVER, "BuildingBlocks", "Ecommerce.Contracts.Grpc", "Protos")
    text = header("gRPC", (
        "The synchronous calls between services. Each runs over h2c on the serving service's second port - "
        "one plaintext port cannot carry HTTP/1.1 and HTTP/2 (see "
        "[service-to-service communication](../architecture/service-to-service-communication.md)). "
        "Every call is asked live: none of them is cached."))
    sources = {path: read(path) for path in files(os.path.join(SERVER, "Services"), ".cs")}
    for path in sorted(files(protos, ".proto")):
        proto = read(path)
        for service, body in re.findall(r"service\s+(\w+)\s*\{(.*?)\}", proto, re.S):
            servers = sorted({service_of(p) for p, s in sources.items() if re.search(rf"{service}\.{service}Base", s)})
            callers = sorted({service_of(p) for p, s in sources.items() if f"{service}.{service}Client" in s})
            text += f"## `{service}` ({os.path.basename(path)})\n\nServed by **{', '.join(servers) or '-'}**, called by **{', '.join(callers) or '-'}**.\n\n"
            text += "| RPC | Request | Response |\n| :-- | :-- | :-- |\n"
            for rpc, request, response in re.findall(r"rpc\s+(\w+)\s*\((\w+)\)\s*returns\s*\((\w+)\)", body):
                text += f"| `{rpc}` | `{request}` | `{response}` |\n"
            text += "\n"
    write("grpc.md", text)


# ------------------------------------------------------------------------------------------ data model

ENTITY = re.compile(r'modelBuilder\.Entity\("([\w.]+)", b =>\s*\{(.*?)\n\s{16}\}\);', re.S)
PROPERTY = re.compile(r'\bb1?\.Property<([\w?.\[\]<>]+)>\("(\w+)"\)(.*?);', re.S)
MASS_TRANSIT = {"InboxState", "OutboxMessage", "OutboxState"}


def snapshot_tables(path):
    tables = defaultdict(lambda: {"columns": {}, "entity": None})
    for entity, body in ENTITY.findall(read(path)):
        table = re.search(r'\bb\.ToTable\("(\w+)"', body)
        if not table:
            continue
        name = table.group(1)
        tables[name]["entity"] = entity.rsplit(".", 1)[-1]
        # The owned type blocks (b1) map into the same table unless they name another one.
        for clr, column, chain in PROPERTY.findall(body):
            renamed = re.search(r'HasColumnName\("(\w+)"\)', chain)
            column_type = re.search(r'HasColumnType\("([^"]+)"\)', chain)
            tables[name]["columns"][renamed.group(1) if renamed else column] = {
                "type": column_type.group(1) if column_type else clr,
                "required": ".IsRequired()" in chain or (not clr.endswith("?") and clr not in ("string", "byte[]")),
                "key": ".ValueGeneratedOnAdd()" in chain and column == "Id",
            }
    return tables


def data_model_page():
    text = header("Data model", (
        "Every table in every service's database, read from the EF Core model snapshot - so it is the "
        "schema the migrations produce. Each service owns its database outright; nothing joins across "
        "them, and a value that crosses a service boundary (a product id in an order line, say) is a copy, "
        "not a foreign key. The MassTransit outbox and inbox tables (`InboxState`, `OutboxMessage`, "
        "`OutboxState`) are in every database that publishes or consumes and are listed once here rather "
        "than per service."))
    total = 0
    for service in SERVICES:
        snapshots = [p for p in files(os.path.join(SERVER, "Services", service), "ModelSnapshot.cs")]
        if not snapshots:
            continue
        tables = snapshot_tables(snapshots[0])
        own = {n: t for n, t in tables.items() if n not in MASS_TRANSIT}
        total += len(own)
        db = f"ecommerce_{service.lower().replace('orchestrator', 'saga')}_db"
        text += f"## {service} - `{db}` ({len(own)} tables)\n\n"
        for name in sorted(own):
            table = own[name]
            text += f"### `{name}`\n\nEntity `{table['entity']}`.\n\n| Column | Type | Null |\n| :-- | :-- | :-- |\n"
            for column, meta in table["columns"].items():
                text += f"| `{column}` | {meta['type']} | {'' if meta['required'] else 'yes'} |\n"
            text += "\n"
    write("data-model.md", text)
    return total


# ------------------------------------------------------------------------------------------ gateway

def gateway_page():
    config = json.loads(read(os.path.join(SERVER, "ApiGateway", "Ecommerce.ApiGateway", "appsettings.json")))
    proxy = config["ReverseProxy"]
    clusters = {name: ", ".join(d["Address"] for d in c["Destinations"].values()) for name, c in proxy["Clusters"].items()}
    text = header("Gateway routes", (
        "What the YARP gateway on `:5000` forwards, and where. The storefront and Bruno talk only to the "
        "gateway. A new endpoint under a new path prefix needs a route here, or it is a 404 that looks like "
        "a missing feature. Container destinations are overridden by command-line arguments in "
        "`docker-compose.app.yml`; the addresses below are the local-development ones."))
    text += "| Route | Path | Cluster | Destination | Rewrites to |\n| :-- | :-- | :-- | :-- | :-- |\n"
    for name, route in sorted(proxy["Routes"].items(), key=lambda kv: (kv[1]["ClusterId"], kv[1]["Match"]["Path"])):
        rewrite = next((t.get("PathPattern") for t in route.get("Transforms", []) if "PathPattern" in t), "")
        text += (f"| `{name}` | `{route['Match']['Path']}` | `{route['ClusterId']}` | "
                 f"{clusters.get(route['ClusterId'], '')} | {f'`{rewrite}`' if rewrite else ''} |\n")
    write("gateway.md", text)


if __name__ == "__main__":
    count = api_page()
    messages = messages_page()
    grpc_page()
    tables = data_model_page()
    gateway_page()
    print(f"{count} endpoints, {messages} messages, {tables} tables")
