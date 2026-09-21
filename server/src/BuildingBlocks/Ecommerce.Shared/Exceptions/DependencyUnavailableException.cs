namespace Ecommerce.Shared.Exceptions;

/// <summary>
/// A service this one depends on could not be reached, so the request is refused.
/// </summary>
/// <remarks>
/// <para>
/// Maps to <b>503 Service Unavailable</b>, and the distinction from 500 is the whole reason this
/// type exists. A 500 says <i>we are broken</i>; this says <i>a dependency is down, try again
/// shortly</i>. They lead a caller to do different things and they lead a monitor to page different
/// people.
/// </para>
/// <para>
/// Until feature 009 nothing in this system had a dependency that could be unavailable - every
/// cross-service interaction was a message, delivered whenever the broker managed it. Order asking
/// Catalog for a price is the first synchronous call, and refusing an order because Catalog cannot
/// answer must not be reported as the order service failing.
/// </para>
/// <para>
/// <b>It must also stay distinguishable from <see cref="NotFoundException"/>.</b> "This product does
/// not exist" and "I could not find out whether this product exists" are different facts; a customer
/// told the first when the second is true goes away and checks a catalogue that is working
/// perfectly.
/// </para>
/// </remarks>
public class DependencyUnavailableException(string message) : Exception(message);
