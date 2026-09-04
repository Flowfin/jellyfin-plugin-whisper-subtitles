# A pull request that cannot be merged, on purpose

The last condition of #54 is that a pull request with a deliberately failing check
cannot be merged, verified by attempting it rather than by reading the settings.
This file is the change such a pull request needs in order to exist.

The check made to fail is `DCO sign-off`, which is one of the contexts the ruleset
on `master` requires. The commit carrying this file has no `Signed-off-by:`
trailer, so that check is red by construction rather than by accident, and it goes
red in seconds without occupying a runner for long.

Nothing here is proposed for the mainline. The pull request is a draft, its branch
begins with `probe/` so a drain leaves it alone, and it is closed rather than
merged once the attempt has been made and recorded on #54.
