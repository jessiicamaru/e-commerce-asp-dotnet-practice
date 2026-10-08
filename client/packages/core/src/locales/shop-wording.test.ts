import { describe, expect, it } from 'vitest'
import enCatalog from './en/catalog.json'
import enCommon from './en/common.json'
import enSeller from './en/seller.json'
import viCatalog from './vi/catalog.json'
import viCommon from './vi/common.json'
import viSeller from './vi/seller.json'

/**
 * The shop's own sentences assume nothing about what it sells (specs/156, #359).
 *
 * <p>
 * The shop sells anything - a variant's options are free text, categories are rows - but it was built with cameras as
 * its example, and its words said so: "Search cameras…", "Take the picture you are looking at.", "Sell your own cameras
 * and lenses here." These bundles are what every shopper and every new seller reads, so a camera word in them is a
 * claim about the whole shop. Product names come from the catalogue and test fixtures may say anything; neither is
 * read here.
 * </p>
 */

const BUNDLES = {
  'en/common': enCommon,
  'en/catalog': enCatalog,
  'en/seller': enSeller,
  'vi/common': viCommon,
  'vi/catalog': viCatalog,
  'vi/seller': viSeller,
}

/** Words for one kind of goods. Whole words, so "lenses" is caught and "photograph" is not. */
const ONE_KIND_OF_GOODS = /\b(cameras?|lens(es)?)\b|máy ảnh|ống kính/i

/** Every string in a bundle with its dotted key. */
function strings(value: unknown, path = ''): [string, string][] {
  if (typeof value === 'string') {
    return [[path, value]]
  }

  if (value && typeof value === 'object') {
    return Object.entries(value).flatMap(([key, child]) => strings(child, path ? `${path}.${key}` : key))
  }

  return []
}

describe("the shop's own wording (specs/156)", () => {
  it.each(Object.entries(BUNDLES))('%s names no kind of goods', (_, bundle) => {
    const offending = strings(bundle).filter(([, text]) => ONE_KIND_OF_GOODS.test(text))

    expect(offending).toEqual([])
  })

  it('reads every string, nested ones included', () => {
    // The guard above is only as good as the walk: the hero's heading is two levels down.
    expect(strings(enCatalog)).toContainEqual(['hero.heading', enCatalog.hero.heading])
    expect(ONE_KIND_OF_GOODS.test('Search cameras…')).toBe(true)
    expect(ONE_KIND_OF_GOODS.test('Bán máy ảnh và ống kính')).toBe(true)
    expect(ONE_KIND_OF_GOODS.test('A photograph of the product')).toBe(false)
  })
})
