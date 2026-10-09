import { slugOf } from '@ecommerce/core/utils/shared'

/** "Tiếng Việt | English" per line, the English optional. */
export function parseOptionLines(text: string): { code: string; value: string; english: string }[] {
  return text
    .split('\n')
    .map((line) => line.split('|').map((part) => part.trim()))
    .filter(([value]) => !!value)
    .map(([value, english = '']) => ({ code: slugOf(value), value, english }))
}
