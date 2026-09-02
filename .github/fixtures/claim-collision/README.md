# Two sets of claim records, one apart from the other

`colliding/` is two plugins that claim one scheduled task key. It is what
`.github/scripts/refuse-a-claim-collision.sh` exists to refuse, and the job that
runs it asserts that the refusal names both claimants and the key rather than
only reporting that something clashed.

`neighbouring/` is the same two records with one value changed: the sibling's
task key. Nothing else differs, and the scan passes it. Without that leg the
refusal above is satisfied by a scan that refuses every set it is handed, which
would be red on this repository's own record too and would be switched off within
a week.

The sibling in both is invented. It is not one of the plugins of this family and
its GUID is not any plugin's: what the fixtures are about is the comparison,
and naming a real sibling would make them go stale the day that sibling changed
a key of its own.

`interoperability/claims/` is this repository's real record, and the job runs the
same scan over it. That leg is the one that would notice this plugin claiming two
things under one name.
