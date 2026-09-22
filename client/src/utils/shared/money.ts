/** One currency, as the backend assumes; formatted, never computed with. */
export const money = (value: number) =>
  value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
