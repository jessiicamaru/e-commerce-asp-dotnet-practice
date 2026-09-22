/** Why a cart line cannot be bought, in words a shopper understands. Null when it can. */
export function lineProblem(status: string): string | null {
  switch (status) {
    case 'Available':
      return null
    case 'NotForSale':
      return 'No longer for sale.'
    case 'NoLongerAvailable':
      return 'This product has been removed from the shop.'
    case 'PriceUnavailable':
      return 'The price could not be checked right now.'
    default:
      return 'This item cannot be bought right now.'
  }
}
