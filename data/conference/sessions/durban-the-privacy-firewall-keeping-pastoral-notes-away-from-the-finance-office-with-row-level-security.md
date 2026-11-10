# The privacy firewall: keeping pastoral notes away from the finance office with row-level security

A church needs one system for two very different kinds of sensitive data: who is in counselling, and who gave what. ChERP, a church management and accounting platform, keeps them apart with a privacy firewall enforced in the database itself, not in the screens. Finance roles never see pastoral records, not even as a locked folder, because Postgres row-level security filters every query before any data comes back. The same mechanism keeps every church isolated from every other church on the platform.

In 15 minutes: why access rules in the UI fail (reports, exports and APIs route around them), how row-level security policies close that gap, and how to do the same in .NET with Npgsql by setting the tenant and role per connection, instead of relying only on EF Core global query filters.
