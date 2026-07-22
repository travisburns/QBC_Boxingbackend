# Owner CRM (customer admin)

A basic, read-only customer view for the gym owner: who's a member, what plan
they're on, their status, and membership history. Deliberately minimal — no
scheduling, POS, or reporting.

## Endpoints (Admin role only)

- `GET /api/admin/customers?search=` — list of customers with plan/status, plus
  total and active-member counts.
- `GET /api/admin/customers/{id}` — one customer's profile + membership history.

Both are gated by `[Authorize(Roles = "Admin")]`. A regular member's token has no
Admin role claim, so these return `403`.

## Granting admin access

1. Add the owner's email to config (never in the repo — use env vars / user-secrets):

   ```
   Admin__Emails__0=owner@qbcboxing.com
   ```

   (add `Admin__Emails__1`, `__2`, … for additional staff)

2. Make sure that email has a **registered account** on the site.
3. Restart the API. On startup it ensures the `Admin` role exists and promotes
   the listed emails. If the account doesn't exist yet, it's promoted on the next
   restart after they register.

## Frontend

The **Admin** link appears in the site header only when the logged-in user has the
Admin role. It links to `/admin` (customer list) and `/admin/customer?id=…`
(detail). Non-admins are redirected away from those pages.

> Access control is enforced on the **API** (role-checked JWT), not just the UI —
> hiding the link is convenience, the `403` is the real guard.
