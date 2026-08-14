-- Seed data for Ledgerly demo
-- Run with: psql -d ledgerly -f docs/seed.sql

INSERT INTO "Tenants" ("Id", "Name", "Slug", "Plan", "PlanStatus", "CreatedAt", "UpdatedAt")
VALUES
  ('11111111-1111-1111-1111-111111111111', 'Demo Agency', 'demo-agency', 1, 1, NOW(), NULL),
  ('22222222-2222-2222-2222-222222222222', 'Sample Studio', 'sample-studio', 0, 1, NOW(), NULL)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Clients" ("Id", "TenantId", "Name", "Email", "Address", "Currency", "CreatedAt")
VALUES
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', '11111111-1111-1111-1111-111111111111', 'Acme Corp', 'billing@acme.test', '123 Main St', 'USD', NOW()),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2', '11111111-1111-1111-1111-111111111111', 'Globex', 'finance@globex.test', NULL, 'USD', NOW()),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3', '11111111-1111-1111-1111-111111111111', 'Initech', 'ap@initech.test', NULL, 'USD', NOW()),
  ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1', '22222222-2222-2222-2222-222222222222', 'Local Coffee', 'hi@localcoffee.test', '1 Bean Lane', 'USD', NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Invoices" ("Id", "TenantId", "ClientId", "Number", "IssueDate", "DueDate", "Status", "Currency", "Subtotal", "Tax", "Total", "PublicPayToken", "CreatedAt")
VALUES
  ('cccccccc-cccc-cccc-cccc-cccccccccc01', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', 'INV-2026-1001', '2026-07-01', '2026-07-15', 1, 'USD', 1500.00, 150.00, 1650.00, 'demo-token-001', NOW()),
  ('cccccccc-cccc-cccc-cccc-cccccccccc02', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2', 'INV-2026-1002', '2026-07-10', '2026-07-24', 2, 'USD',  800.00,  80.00,  880.00, 'demo-token-002', NOW()),
  ('cccccccc-cccc-cccc-cccc-cccccccccc03', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3', 'INV-2026-1003', '2026-08-01', '2026-08-15', 0, 'USD',  450.00,   0.00,  450.00, 'demo-token-003', NOW()),
  ('cccccccc-cccc-cccc-cccc-cccccccccc04', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1', 'INV-2026-2001', '2026-08-01', '2026-08-15', 1, 'USD',  200.00,   0.00,  200.00, 'demo-token-201', NOW()),
  ('cccccccc-cccc-cccc-cccc-cccccccccc05', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1', 'INV-2026-2002', '2026-08-10', '2026-08-24', 3, 'USD',  120.00,   0.00,  120.00, 'demo-token-202', NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "InvoiceLines" ("Id", "InvoiceId", "Description", "Quantity", "UnitPrice", "TaxRate", "CreatedAt")
VALUES
  ('dddddddd-dddd-dddd-dddd-dddddddddd01', 'cccccccc-cccc-cccc-cccc-cccccccccc01', 'Landing page', 1, 1500.00, 10, NOW()),
  ('dddddddd-dddd-dddd-dddd-dddddddddd02', 'cccccccc-cccc-cccc-cccc-cccccccccc02', 'Logo refresh', 1,  800.00, 10, NOW()),
  ('dddddddd-dddd-dddd-dddd-dddddddddd03', 'cccccccc-cccc-cccc-cccc-cccccccccc03', 'SEO audit',   3,  150.00,  0, NOW()),
  ('dddddddd-dddd-dddd-dddd-dddddddddd04', 'cccccccc-cccc-cccc-cccc-cccccccccc04', 'Catering',    1,  200.00,  0, NOW()),
  ('dddddddd-dddd-dddd-dddd-dddddddddd05', 'cccccccc-cccc-cccc-cccc-cccccccccc05', 'Top-ups',     2,   60.00,  0, NOW())
ON CONFLICT ("Id") DO NOTHING;