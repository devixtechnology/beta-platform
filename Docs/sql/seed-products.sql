-- =============================================================================
-- Beta Platform - products from the ERP catalogue export (29 rows)
-- Generated 2026-09-15 from the Odoo /products payload.
--
-- HOW THE PAYLOAD MAPS ONTO `products`
--   product_code         <- the Odoo `id`, as text ('109', '110', ... '160').
--                           The payload carries no product code, and product_code
--                           is UNIQUE NOT NULL and is the identifier the API
--                           exposes (inputProductCodes / outputProductCode).
--                           The Odoo id is used because 8 names are duplicated
--                           in this payload (see NOTE 1) - names cannot identify.
--   product_name         <- `name`, verbatim.
--   product_name_english <- `name` when it is Latin script, NULL for the 5
--                           Arabic names (151, 153, 154, 139, 160).
--   unit                 <- 'kg' for every row. The payload gives only a numeric
--                           uom_id (1, 9, 16) with no lookup table; 'kg' is the
--                           entity default (Product.Unit) and matches the two
--                           rows already in the table. SEE NOTE 2.
--   category             <- NULL (not present in the payload).
--   is_active            <- 1.
--   created_at           <- fixed timestamp, so re-generating is reproducible.
--   product_id           <- left to AUTO_INCREMENT. The internal id is never
--                           exposed by the API, so it is deliberately not tied
--                           to the Odoo id; product_code carries that identity.
--
-- FIELDS IN THE PAYLOAD THAT ARE DROPPED - the schema has nowhere to put them:
--   bill_of_material   - there is no BOM table in this database at all. Products
--                        109, 136 and 154 carry a BOM and it is NOT stored here.
--                        SEE NOTE 3.
--   qty_on_hand        - no stock column on `products`. Values preserved as a
--                        line comment on each row so nothing is lost.
--   uom_id             - no column; collapsed into `unit` as above.
--   weight_per_washer, holes_per_mold, weight_per_rod - no columns. All are 0
--                        in this payload, so nothing is lost by dropping them.
--
-- Re-runnable: ON DUPLICATE KEY UPDATE keyed on the unique product_code index.
-- Re-running refreshes name/unit/is_active and never duplicates a row.
--
-- Usage - the client must speak utf8mb4 or the Arabic names become '?':
--   mysql --default-character-set=utf8mb4 -u <user> -p <database> < seed-products.sql
-- =============================================================================

SET NAMES utf8mb4;

START TRANSACTION;

INSERT INTO products
  (product_code, product_name, product_name_english, category, unit, is_active, created_at)
VALUES
  ('109', 'Armor rod 4.24mm, AA 6061', 'Armor rod 4.24mm, AA 6061', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 109 uom 16 qty_on_hand 278.0
  ('110', 'Armor rod 5.18mm, AA 6061', 'Armor rod 5.18mm, AA 6061', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 110 uom 16 qty_on_hand 0.0
  ('111', 'Guy Grip 3.51mm,GI,new', 'Guy Grip 3.51mm,GI,new', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 111 uom 16 qty_on_hand 100.0
  ('112', 'Guy Grip 3.02mm,GI,new', 'Guy Grip 3.02mm,GI,new', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 112 uom 16 qty_on_hand 100.0
  ('113', 'carbon steel round bar 18.2mm, ANSI 1045', 'carbon steel round bar 18.2mm, ANSI 1045', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 113 uom 16 qty_on_hand 0.0
  ('114', 'Washers material for all sizes, S235', 'Washers material for all sizes, S235', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 114 uom 16 qty_on_hand 0.0
  ('115', 'carbon steel round bar 28mm, ANSI 1045', 'carbon steel round bar 28mm, ANSI 1045', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 115 uom 16 qty_on_hand 0.0
  ('116', 'Guy Grip 3.02mm,GI,OLD LESS Tensile strength', 'Guy Grip 3.02mm,GI,OLD LESS Tensile strength', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 116 uom 16 qty_on_hand 0.0
  ('117', 'HEX BOLT /HEX NUT for EG', 'HEX BOLT /HEX NUT for EG', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 117 uom 16 qty_on_hand 0.0
  ('128', 'Armor rod 5.18mm, AA 6061', 'Armor rod 5.18mm, AA 6061', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 128 uom 16 qty_on_hand 0.0
  ('129', 'Guy Grip 3.51mm,GI,new', 'Guy Grip 3.51mm,GI,new', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 129 uom 16 qty_on_hand 20.0
  ('130', 'Guy Grip 3.02mm,GI,new', 'Guy Grip 3.02mm,GI,new', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 130 uom 16 qty_on_hand 0.0
  ('131', 'carbon steel round bar 18.2mm, ANSI 1045', 'carbon steel round bar 18.2mm, ANSI 1045', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 131 uom 16 qty_on_hand 0.0
  ('132', 'Washers material for all sizes, S235', 'Washers material for all sizes, S235', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 132 uom 16 qty_on_hand 0.0
  ('133', 'carbon steel round bar 28mm, ANSI 1045', 'carbon steel round bar 28mm, ANSI 1045', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 133 uom 16 qty_on_hand 0.0
  ('134', 'Guy Grip 3.02mm,GI,OLD LESS Tensile strength', 'Guy Grip 3.02mm,GI,OLD LESS Tensile strength', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 134 uom 16 qty_on_hand 0.0
  ('135', 'HEX BOLT /HEX NUT for EG', 'HEX BOLT /HEX NUT for EG', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 135 uom 16 qty_on_hand 0.0
  ('136', 'Table', 'Table', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 136 uom 1  qty_on_hand 1.0
  ('137', 'Table Top', 'Table Top', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 137 uom 1  qty_on_hand 0.0
  ('138', 'Table Leg', 'Table Leg', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 138 uom 1  qty_on_hand 0.0
  ('139', 'سيلكون', NULL, NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 139 uom 1  qty_on_hand 450.0
  ('140', 'TEST Product', 'TEST Product', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 140 uom 1  qty_on_hand 13.0
  ('151', 'تليفون', NULL, NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 151 uom 1  qty_on_hand 0.0
  ('152', 'dd', 'dd', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 152 uom 1  qty_on_hand 5.0
  ('153', 'سلك الومنيوم2م', NULL, NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 153 uom 9  qty_on_hand 400.0
  ('154', 'شدادالومنيوم', NULL, NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 154 uom 1  qty_on_hand 60.0
  ('157', 'A', 'A', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 157 uom 1  qty_on_hand 222.0
  ('158', 'B', 'B', NULL, 'kg', 1, '2026-09-15 00:00:00.000000'),  -- odoo id 158 uom 1  qty_on_hand 0.0
  ('160', 'خ', NULL, NULL, 'kg', 1, '2026-09-15 00:00:00.000000')  -- odoo id 160 uom 1  qty_on_hand 0.0
ON DUPLICATE KEY UPDATE
  product_name         = VALUES(product_name),
  product_name_english = VALUES(product_name_english),
  unit                 = VALUES(unit),
  is_active            = VALUES(is_active);

COMMIT;

-- -----------------------------------------------------------------------------
-- NOTE 1 - duplicate names in the source payload
--   These 8 names each arrive twice under different Odoo ids. They become 16
--   separate product rows with distinct codes; decide in the ERP whether they
--   are genuinely distinct products or duplicates to be merged:
--     110 / 128  Armor rod 5.18mm, AA 6061
--     116 / 134  Guy Grip 3.02mm,GI,OLD LESS Tensile strength
--     112 / 130  Guy Grip 3.02mm,GI,new
--     111 / 129  Guy Grip 3.51mm,GI,new
--     117 / 135  HEX BOLT /HEX NUT for EG
--     114 / 132  Washers material for all sizes, S235
--     113 / 131  carbon steel round bar 18.2mm, ANSI 1045
--     115 / 133  carbon steel round bar 28mm, ANSI 1045
--
-- NOTE 2 - unit is 'kg' for all 29 rows, which is certainly wrong for some
--   (a Table and a telephone are not weighed in kg). To correct it once you have
--   the Odoo uom names, run something like:
--     UPDATE products SET unit = 'Units' WHERE product_code IN
--       ('157','158','140','136','138','137','152','151','160','139','154');
--     UPDATE products SET unit = 'm'     WHERE product_code IN ('153');
--   Get the real names with:  SELECT id, name FROM uom_uom WHERE id IN (1,9,16);
--
-- NOTE 3 - bills of material are not imported
--   Product 109 -> 1 x Odoo product 60  '[M10003] Armor rod 5.18mm, AA 6061'
--   Product 136 -> 1 x Odoo product 87  '[SAMPLE_TABLE_TOP] Table Top'
--                  4 x Odoo product 88  '[SAMPLE_TABLE_LEG] Table Leg'
--   Product 154 -> 10 x Odoo product 103 '[M10030] Alu wire 2m'
--   Two problems beyond the missing table: the components are addressed by Odoo
--   ids 60, 87, 88 and 103, none of which appear in this payload, so they would
--   be dangling references; and those display names reveal that real product
--   codes (M10003, M10030, SAMPLE_TABLE_TOP) do exist in the ERP. If you can
--   export those codes, they are a better product_code than the bare Odoo id.
-- -----------------------------------------------------------------------------

-- Verify
-- SELECT product_code, product_name, product_name_english, unit, is_active
-- FROM products ORDER BY CAST(product_code AS UNSIGNED);
