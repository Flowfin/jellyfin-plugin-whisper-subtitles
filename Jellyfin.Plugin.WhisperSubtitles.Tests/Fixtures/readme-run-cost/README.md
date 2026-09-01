# A run that folds a measurement in, and one that only reads one back

`ReadmeRunCostTests` searches this plugin's sources for a caller of the arithmetic
that folds a measured item into a throughput factor, because the README denies that
one exists. These two prove the search bites for that reason and not for a
neighbouring one.

- `records-a-measured-item.cs.fixture` is the caller. It is the shape the joining in
  #183 would arrive as: the run already holds the ledger, and one call at the end of
  an item is the whole of the change that makes the README's sentence false.
- `reads-a-measurement-back.cs.fixture` is the neighbour that has to stay accepted,
  and it is not invented. `Estimation/DryRun.cs` names the ledger and takes a
  measurement out of it, which is a reading rather than a folding, so a search
  matching the ledger's name alone would look right and would refuse this plugin as
  it stands today.

The extension keeps them out of the compilation and out of the way of anything that
walks the tree for sources. Neither has to compile: the search reads text, so a
fixture that compiled would prove nothing more.
