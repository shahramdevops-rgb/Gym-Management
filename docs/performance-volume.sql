-- Three years of a busy gym, for EXPLAIN only (task 11.3, docs/performance-review.md).
-- Runs against a throwaway Postgres started for the purpose, NEVER against the development
-- database or the server: it inserts about 850,000 rows. Needs the "perfowner" account the API seeds
-- (Seed__OwnerUserName=perfowner) and the lockers and expense categories the migrations seed.
\set ON_ERROR_STOP on
BEGIN;

CREATE TEMP TABLE owner AS SELECT id FROM users WHERE user_name = 'perfowner';
CREATE TEMP TABLE locker_ids AS SELECT row_number() OVER (ORDER BY number) AS n, id FROM lockers;
CREATE TEMP TABLE category_ids AS SELECT row_number() OVER (ORDER BY id) AS n, id FROM expense_categories;

-- 3,000 members joining evenly from 2023-10-10 to 2026-10-10.
CREATE TEMP TABLE m AS
SELECT uuidv7() AS id, g,
       (date '2023-10-10' + (g * 1096 / 3000)) AS joined
FROM generate_series(1, 3000) g;

INSERT INTO members (id, full_name, normalized_full_name, phone_number, is_active, created_at, birth_date)
SELECT id, 'عضو شماره ' || g, 'عضو شماره ' || g, '+98912' || lpad(g::text, 7, '0'), g % 10 <> 0,
       joined + time '10:00' - interval '3 hours 30 minutes', date '1980-01-01' + (g % 9000)
FROM m;

-- 1 to 6 consecutive 10-session plans each, 35 days apart.
CREATE TEMP TABLE s AS
SELECT uuidv7() AS id, m.id AS member_id, m.g, k,
       m.joined + (k - 1) * 35 AS start_date
FROM m, generate_series(1, 1 + m.g % 6) k;

INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions, start_date, end_date,
    used_sessions, total_frozen_days, created_at, is_single_session)
SELECT id, member_id, 900000, 30, 10, start_date, start_date + 29,
       least(10, greatest(0, (date '2026-10-10' - start_date) / 3)), 0,
       start_date + time '10:05' - interval '3 hours 30 minutes', false
FROM s;

-- One visit every third day of a plan, closed, until yesterday.
CREATE TEMP TABLE a AS
SELECT uuidv7() AS id, s.member_id, s.id AS subscription_id, s.g, i,
       (s.start_date + i * 3) + time '17:00' - interval '3 hours 30 minutes' AS checked_in_at
FROM s, generate_series(0, 9) i
WHERE s.start_date + i * 3 < date '2026-10-10';

INSERT INTO attendances (id, member_id, subscription_id, locker_id, checked_in_at, checked_out_at, created_at)
SELECT a.id, a.member_id, a.subscription_id, l.id, a.checked_in_at, a.checked_in_at + interval '90 minutes', a.checked_in_at
FROM a JOIN locker_ids l ON l.n = 1 + (a.g + a.i) % 72;

-- Guests: about five a day.
INSERT INTO attendances (id, guest_name, locker_id, checked_in_at, checked_out_at, created_at)
SELECT uuidv7(), 'مهمان ' || g, l.id, t, t + interval '1 hour', t
FROM (SELECT g, timestamptz '2023-10-10 14:00+00' + (g * interval '1 day' / 5) AS t FROM generate_series(1, 5480) g) x
JOIN locker_ids l ON l.n = 1 + g % 72;

-- A cardio charge on one visit in five, half of them paid.
CREATE TEMP TABLE c AS
SELECT uuidv7() AS id, a.id AS attendance_id, a.member_id, a.checked_in_at
FROM a WHERE (a.g + a.i) % 5 = 0;

INSERT INTO service_charges (id, member_id, attendance_id, kind, amount, charged_on, recorded_by_user_id, created_at)
SELECT c.id, c.member_id, c.attendance_id, 'Cardio', 50000, (c.checked_in_at AT TIME ZONE 'Asia/Tehran')::date, owner.id, c.checked_in_at
FROM c, owner;

-- A cafe order on one visit in four, with two lines.
INSERT INTO product_categories (id, name, normalized_name, is_active, created_at)
VALUES (uuidv7(), 'نوشیدنی', 'نوشیدنی', true, now());

INSERT INTO products (id, name, normalized_name, category_id, price, is_active, created_at)
SELECT uuidv7(), 'محصول ' || g, 'محصول ' || g, (SELECT id FROM product_categories LIMIT 1), 30000 + g * 10000, true, now()
FROM generate_series(1, 8) g;
CREATE TEMP TABLE product_ids AS SELECT row_number() OVER (ORDER BY id) AS n, id, name, price FROM products;

CREATE TEMP TABLE o AS
SELECT uuidv7() AS id, a.id AS attendance_id, a.member_id, a.checked_in_at, a.g, a.i
FROM a WHERE (a.g + a.i) % 4 = 0;

INSERT INTO cafe_orders (id, member_id, total_amount, ordered_on, placed_by_user_id, created_at, attendance_id)
SELECT o.id, o.member_id, 0, (o.checked_in_at AT TIME ZONE 'Asia/Tehran')::date, owner.id, o.checked_in_at, o.attendance_id
FROM o, owner;

INSERT INTO cafe_order_items (id, order_id, product_id, product_name, unit_price, quantity, line_total, created_at)
SELECT uuidv7(), o.id, p.id, p.name, p.price, q, p.price * q, o.checked_in_at
FROM o, generate_series(1, 2) q JOIN product_ids p ON true
WHERE p.n = 1 + (o.g + o.i + q) % 8;

UPDATE cafe_orders SET total_amount = t.total
FROM (SELECT order_id, sum(line_total) AS total FROM cafe_order_items GROUP BY order_id) t
WHERE t.order_id = cafe_orders.id;

-- Payments: each plan in full, half the cardio charges, nine orders in ten.
INSERT INTO payments (id, subscription_id, kind, amount, method, paid_at, received_by_user_id, created_at)
SELECT uuidv7(), s.id, 'Payment', 900000, CASE WHEN s.g % 3 = 0 THEN 'Card' ELSE 'Cash' END,
       s.start_date + time '10:06' - interval '3 hours 30 minutes', owner.id, s.start_date + time '10:06' - interval '3 hours 30 minutes'
FROM s, owner;

INSERT INTO payments (id, service_charge_id, kind, amount, method, paid_at, received_by_user_id, created_at)
SELECT uuidv7(), c.id, 'Payment', 50000, 'Cash', c.checked_in_at, owner.id, c.checked_in_at
FROM c, owner WHERE c.attendance_id::text < '8';

INSERT INTO payments (id, cafe_order_id, kind, amount, method, paid_at, received_by_user_id, created_at)
SELECT uuidv7(), o.id, 'Payment', co.total_amount, 'Cash', o.checked_in_at, owner.id, o.checked_in_at
FROM o JOIN cafe_orders co ON co.id = o.id, owner WHERE (o.g + o.i) % 10 <> 0;

-- An expiring-plan SMS for each plan.
INSERT INTO notifications (id, kind, recipient, member_id, subscription_id, status, attempts, last_attempt_at,
    provider_message_id, cost_rial, sent_at, delivery, created_at, text)
SELECT uuidv7(), 'SubscriptionExpiring', '+98912' || lpad(s.g::text, 7, '0'), s.member_id, s.id, 'Sent', 1,
       t, 1000000 + row_number() OVER (), 1500, t, 'Delivered', t, 'اشتراک شما رو به پایان است'
FROM (SELECT s.*, (s.start_date + 27) + time '10:00' - interval '3 hours 30 minutes' AS t FROM s
      WHERE s.start_date + 27 < date '2026-10-10') s;

-- Expenses: one a day. Payables: one a week.
INSERT INTO expenses (id, amount, category_id, expense_date, description, recorded_by_user_id, created_at)
SELECT uuidv7(), 200000 + (g % 50) * 10000, ec.id, date '2023-10-10' + g, 'هزینه ' || g, owner.id, now()
FROM generate_series(0, 1095) g JOIN category_ids ec ON ec.n = 1 + g % (SELECT count(*) FROM category_ids), owner;

INSERT INTO payables (id, kind, amount, due_date, payee, description, category_id, registered_by_user_id,
    paid_at, paid_by_user_id, created_at)
SELECT uuidv7(), 'Cheque', 5000000, date '2023-10-10' + g * 7, 'فروشنده ' || g, 'چک', ec.id, owner.id,
       CASE WHEN date '2023-10-10' + g * 7 < date '2026-10-10' THEN now() END,
       CASE WHEN date '2023-10-10' + g * 7 < date '2026-10-10' THEN owner.id END, now()
FROM generate_series(0, 160) g JOIN category_ids ec ON ec.n = 1, owner;

-- Sign-ins: a refresh token every 15 minutes of a 16-hour day, three years.
INSERT INTO refresh_tokens (id, user_id, token_hash, family_id, expires_at, revoked_at, revoked_reason, created_at)
SELECT uuidv7(), owner.id, md5(g::text) || md5((g + 1)::text), uuidv7(),
       t + interval '7 days', t + interval '15 minutes', 'Rotated', t
FROM (SELECT g, timestamptz '2023-10-10 03:00+00' + (g / 64) * interval '1 day' + (g % 64) * interval '15 minutes' AS t
      FROM generate_series(1, 64 * 1096) g) x, owner;

-- The audit log: an insert for every row above, an update for every check-out, and the tokens.
INSERT INTO audit_logs (id, user_id, action, entity_type, entity_id, occurred_at, new_values)
SELECT uuidv7(), owner.id, 'Insert', t.entity_type, t.entity_id, t.at, jsonb_build_object('Id', t.entity_id)
FROM owner, (
          SELECT 'Member' AS entity_type, id::text AS entity_id, created_at AS at FROM members
UNION ALL SELECT 'Subscription', id::text, created_at FROM subscriptions
UNION ALL SELECT 'Attendance', id::text, created_at FROM attendances
UNION ALL SELECT 'ServiceCharge', id::text, created_at FROM service_charges
UNION ALL SELECT 'CafeOrder', id::text, created_at FROM cafe_orders
UNION ALL SELECT 'CafeOrderItem', id::text, created_at FROM cafe_order_items
UNION ALL SELECT 'Payment', id::text, created_at FROM payments
UNION ALL SELECT 'Notification', id::text, created_at FROM notifications
UNION ALL SELECT 'Expense', id::text, created_at FROM expenses
UNION ALL SELECT 'RefreshToken', id::text, created_at FROM refresh_tokens) t;

INSERT INTO audit_logs (id, user_id, action, entity_type, entity_id, occurred_at, old_values, new_values)
SELECT uuidv7(), owner.id, 'Update', 'Attendance', a.id::text, a.checked_out_at,
       jsonb_build_object('CheckedOutAt', null), jsonb_build_object('CheckedOutAt', a.checked_out_at)
FROM attendances a, owner;

INSERT INTO audit_logs (id, user_id, action, entity_type, entity_id, occurred_at, old_values, new_values)
SELECT uuidv7(), owner.id, 'Update', 'RefreshToken', r.id::text, r.revoked_at,
       jsonb_build_object('RevokedAt', null), jsonb_build_object('RevokedAt', r.revoked_at)
FROM refresh_tokens r, owner;

COMMIT;
ANALYZE;

SELECT relname, n_live_tup FROM pg_stat_user_tables WHERE n_live_tup > 100 ORDER BY n_live_tup DESC;
