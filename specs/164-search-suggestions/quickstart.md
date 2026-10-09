# Quickstart: Search suggestions while typing

Against the compose stack rebuilt from this branch, with the seeded catalogue.

```bash
curl -s "localhost:5000/api/products/suggest?q=may%20anh&lang=vi" | jq '.categories[].name, .products[].name'
curl -s "localhost:5000/api/products/suggest?q=sony&lang=en" | jq '.products | length'
curl -s -o /dev/null -w "%{http_code}\n" "localhost:5000/api/products/suggest?q=s"
```

Expected: "Máy ảnh..." categories and cameras for "may anh"; Sony products (at most 6) for "sony"; 400 for one letter.

In the shop: type "son" in the search box - a dropdown with Sony products; down, down, Enter opens the second; type
"son" and Enter - the search page; Escape closes the dropdown.
