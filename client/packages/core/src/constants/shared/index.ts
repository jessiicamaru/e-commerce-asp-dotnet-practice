/**
 * How many rows every paged list in the storefront asks for: products, listings, orders, sales.
 *
 * ONE number, on purpose. 12 divides evenly into a grid of 4, 3 or 2 across, and the server's queries
 * default to the same, so a request that forgets to say still gets the page this one expects. A
 * constant per page is how three lists came to disagree (12, 12, 10) without anybody deciding it.
 */
export const PAGE_SIZE = 12
