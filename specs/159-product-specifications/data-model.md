# Data Model: Product specifications per category

One migration, `AddProductSpecifications`, adding five tables. Nothing existing changes.

| Table | Columns | Keys and rules |
| :-- | :-- | :-- |
| `category_specifications` | `Id`, `CategoryId`, `Code` (60), `Name` (100), `Kind` (`Text`/`Choice`, string), `Position`, `CreatedAt` | PK `Id`; unique (`CategoryId`, `Code`); FK `CategoryId` → `categories` **cascade** |
| `category_specification_translations` | `SpecificationId`, `Language` (10), `Name` (100) | PK (`SpecificationId`, `Language`); FK cascade |
| `specification_options` | `Id`, `SpecificationId`, `Code` (60), `Value` (100), `Position` | PK `Id`; unique (`SpecificationId`, `Code`); FK cascade |
| `specification_option_translations` | `OptionId`, `Language` (10), `Value` (100) | PK (`OptionId`, `Language`); FK cascade |
| `product_specifications` | `ProductId`, `SpecificationId`, `OptionId` (null), `Text` (200, null) | PK (`ProductId`, `SpecificationId`); FK `ProductId` **cascade**; FK `SpecificationId`, `OptionId` **restrict**; index on `OptionId`; CHECK exactly one of `OptionId`, `Text` |

Rules (Application):

```text
applicable(product)  = specifications of product.Category  +  of product.Category.Parent (if any), department first
a value              = Choice -> an option OF THAT specification ; Text -> 1..200 characters, trimmed
delete specification = 409 while any product_specifications row names it
delete option        = 409 while any product_specifications row names it
```

State: a product's set is replaced whole by its PUT. Values for specifications that stop applying (a category or
product moved) stay in the table and are not shown.
