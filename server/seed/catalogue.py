#!/usr/bin/env python3
"""The seed catalogue: one JSON file per vertical in seed/catalogue/, loaded and checked here (specs/156).

    cd server
    python seed/catalogue.py            # checks every file; exit 1 naming each problem

The shop sells anything - a variant's options are free text and categories are rows - so the demo catalogue is not
one kind of goods either. Each file is a vertical (cameras, phones and laptops, clothing ...) in the format
cameras.json always had (specs/023). The seeder and the cleaner both read them through `load`, so "what the seed
names" has one definition: the cleaner deleting a vertical the seeder had just written is the failure this prevents.

CI runs this file on its own. The seeder needs the whole stack and does not run there; the data it would send is
checked instead, so a duplicate SKU or a dong price with a fraction fails a pull request rather than a demo.
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DIRECTORY = os.path.join(HERE, "catalogue")


class CatalogueError(Exception):
    """Every problem found, not the first: fixing one file a run at a time is how a seed rots."""

    def __init__(self, problems):
        super().__init__("\n".join(problems))
        self.problems = problems


def files(only=()):
    """The vertical files, by name without .json, sorted so every run seeds in the same order."""
    names = sorted(name[:-5] for name in os.listdir(DIRECTORY) if name.endswith(".json"))
    unknown = [name for name in only if name not in names]

    if unknown:
        raise CatalogueError([f"no vertical named {', '.join(unknown)} - there are: {', '.join(names)}"])

    return [name for name in names if not only or name in only]


def _texts(where, block, keys, problems):
    for key in keys:
        if not isinstance(block, dict) or not str(block.get(key) or "").strip():
            problems.append(f"{where}: no {key}")


def _pair(where, value, problems):
    if not (isinstance(value, list) and len(value) == 2 and all(isinstance(v, str) and v.strip() for v in value)):
        problems.append(f"{where}: an option is a [name, value] pair, got {value!r}")
        return False

    return True


def _check(name, data, problems):
    """One file's own rules. Returns its categories and products for the cross-file checks."""
    categories = data.get("categories")
    products = data.get("products")

    if not isinstance(categories, list) or not isinstance(products, list) or not products:
        problems.append(f"{name}: needs a list of categories and a non-empty list of products")
        return [], []

    for category in categories:
        where = f"{name}: category {category.get('slug')!r}"
        _texts(where, category, ["slug", "name"], problems)
        _texts(where + " (en)", category.get("en"), ["name"], problems)

    for product in products:
        where = f"{name}: {product.get('sku')!r}"
        _texts(where, product, ["sku", "category"], problems)
        _texts(where + " (vi)", product.get("vi"), ["name", "description"], problems)
        _texts(where + " (en)", product.get("en"), ["name", "description"], problems)
        variants = product.get("variants")

        if not isinstance(variants, list) or not variants:
            problems.append(f"{where}: no variants")
            continue

        # One product's shapes are described by the same options: "Size, Colour" on one and "Colour" on another is
        # two products in one, and the storefront's chooser cannot offer it.
        names = None

        for variant in variants:
            here = f"{where} variant {variant.get('sku')!r}"
            _texts(here, variant, ["sku"], problems)
            options = variant.get("options")

            if not isinstance(options, list):
                problems.append(f"{here}: options is a list")
                continue

            vi = []
            en = []

            for option in options:
                if _pair(here + " (vi)", option.get("vi"), problems):
                    vi.append(option["vi"][0])
                if _pair(here + " (en)", option.get("en"), problems):
                    en.append(option["en"][0])

            if len(set(vi)) != len(vi):
                problems.append(f"{here}: names an option twice")

            shape = (tuple(sorted(vi)), tuple(sorted(en)))

            if names is None:
                names = shape
            elif shape != names:
                problems.append(f"{here}: names options {list(shape[0])}, another variant of it {list(names[0])}")

            # Dong has no minor unit (specs/022): Catalog refuses 9.99 VND, and so does this, before it gets that far.
            vnd = variant.get("vnd")
            if not (isinstance(vnd, int) and not isinstance(vnd, bool) and vnd > 0):
                problems.append(f"{here}: vnd is a positive whole number of dong, got {vnd!r}")

            usd = variant.get("usd")
            if not (isinstance(usd, (int, float)) and not isinstance(usd, bool) and usd > 0
                    and round(usd, 2) == usd):
                problems.append(f"{here}: usd is a positive amount with at most two decimals, got {usd!r}")

            stock = variant.get("stock")
            if not (isinstance(stock, int) and not isinstance(stock, bool) and stock >= 0):
                problems.append(f"{here}: stock is a whole number, got {stock!r}")

    return categories, products


def load(only=()):
    """Every vertical (or the named ones), merged and checked across files. Raises CatalogueError."""
    problems = []
    merged = {"verticals": [], "categories": [], "products": []}

    for name in files(only):
        try:
            with open(os.path.join(DIRECTORY, name + ".json"), encoding="utf-8") as handle:
                data = json.load(handle)
        except (OSError, ValueError) as error:
            problems.append(f"{name}: cannot be read ({error})")
            continue

        categories, products = _check(name, data, problems)
        merged["verticals"].append((name, len(categories), len(products), sum(len(p.get("variants") or []) for p in products)))
        merged["categories"] += [dict(c, _file=name) for c in categories]
        merged["products"] += [dict(p, _file=name) for p in products]

    # Across files. A SKU is how the seeder decides "already there", and a slug is how it finds a category: a
    # duplicate in two verticals would make the second one quietly attach to the first's rows.
    seen = {}
    for product in merged["products"]:
        for sku in [product.get("sku")] + [v.get("sku") for v in product.get("variants") or []]:
            if sku in seen and seen[sku] != (product["_file"], product.get("sku")):
                problems.append(f"{product['_file']}: SKU {sku!r} is also used in {seen[sku][0]}")
            seen.setdefault(sku, (product["_file"], product.get("sku")))

    slugs = {}
    for category in merged["categories"]:
        slug = category.get("slug")
        if slug in slugs:
            problems.append(f"{category['_file']}: category {slug!r} is also in {slugs[slug]}")
        slugs.setdefault(slug, category["_file"])

    # Departments and their categories, two levels (specs/158): a parent is a category some loaded vertical declares, it
    # is not the category itself, and it has no parent of its own - the same rules the server enforces.
    by_slug = {c.get('slug'): c for c in merged["categories"]}
    for category in merged["categories"]:
        parent = category.get('parent')
        if parent is None:
            continue
        where = f"{category['_file']}: category {category.get('slug')!r}"
        if parent == category.get('slug'):
            problems.append(f"{where} is its own parent")
        elif parent not in by_slug:
            problems.append(f"{where} is under {parent!r}, which no loaded vertical declares")
        elif by_slug[parent].get('parent') is not None:
            problems.append(f"{where} is under {parent!r}, which is itself under {by_slug[parent]['parent']!r} - "
                            "categories go two levels deep")

    # Specifications (specs/159): what a category declares, and what each product fills in - the server's rules again,
    # so a mistake fails here rather than halfway through seeding.
    import re
    code = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")
    declared = {}
    for category in merged["categories"]:
        where = f"{category['_file']}: category {category.get('slug')!r}"
        own = {}
        for spec in category.get("specifications") or []:
            at = f"{where} specification {spec.get('code')!r}"
            if not code.match(str(spec.get("code") or "")):
                problems.append(f"{at}: the code is lower-case words joined by hyphens")
            if spec.get("code") in own:
                problems.append(f"{at} is declared twice")
            _texts(at, spec, ["vi", "en"], problems)
            if spec.get("kind") not in ("Text", "Choice"):
                problems.append(f"{at}: kind is Text or Choice, got {spec.get('kind')!r}")
            options = spec.get("options") or []
            if spec.get("kind") == "Choice" and not options:
                problems.append(f"{at}: a choice needs options")
            if spec.get("kind") == "Text" and options:
                problems.append(f"{at}: a text has no options")
            seen_options = set()
            for option in options:
                if not code.match(str(option.get("code") or "")) or option.get("code") in seen_options:
                    problems.append(f"{at}: option {option.get('code')!r} needs a unique code")
                seen_options.add(option.get("code"))
                _texts(f"{at} option {option.get('code')!r}", option, ["vi", "en"], problems)
            own[spec.get("code")] = spec
        declared[category.get("slug")] = own

    def applicable(slug):
        parent = by_slug.get(slug, {}).get("parent")
        return {**(declared.get(parent) or {}), **(declared.get(slug) or {})}

    for category in merged["categories"]:
        parent = category.get("parent")
        clash = set(declared.get(category.get("slug")) or {}) & set(declared.get(parent) or {})
        if clash:
            problems.append(f"{category['_file']}: category {category.get('slug')!r} redeclares its department's "
                            f"{sorted(clash)} - one code, one specification")

    for product in merged["products"]:
        specs = applicable(product.get("category"))
        for key, value in (product.get("specifications") or {}).items():
            at = f"{product['_file']}: {product.get('sku')!r} specification {key!r}"
            spec = specs.get(key)
            if spec is None:
                problems.append(f"{at} is not declared by its category or department")
            elif spec.get("kind") == "Choice" and value not in {o.get("code") for o in spec.get("options") or []}:
                problems.append(f"{at}: {value!r} is not one of its options")
            elif spec.get("kind") == "Text" and not (isinstance(value, str) and value.strip() and len(value) <= 200):
                problems.append(f"{at}: a text value is 1 to 200 characters")

    # A vertical may file a product under another vertical's category only when that one is loaded too.
    for product in merged["products"]:
        if product.get("category") not in slugs:
            problems.append(f"{product['_file']}: {product.get('sku')!r} is filed under {product.get('category')!r}, "
                            "which no loaded vertical declares")

    if problems:
        raise CatalogueError(problems)

    return merged


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    try:
        merged = load(sys.argv[1:])
    except CatalogueError as error:
        print(f"The seed catalogue has {len(error.problems)} problem(s):")
        for problem in error.problems:
            print(f"  !!  {problem}")
        sys.exit(1)

    for name, categories, products, variants in merged["verticals"]:
        print(f"  ok  {name:<22} {categories} categor(ies), {products} product(s), {variants} variant(s)")

    print(f"  ok  {len(merged['verticals'])} vertical(s), {len(merged['products'])} product(s) in all")


if __name__ == "__main__":
    main()
