# Fixtures the route-claim reader has to sort

`RouteClaimsTests` records the API paths this plugin claims from the server, which
is none, and reads every plugin source against that record. Each file here is one
case the reader has to get right, and each differs from its neighbour in one thing.

`claims-a-route-of-its-own.cs.fixture` is the refusal. It answers a path so the
configuration page can ask whether the selected backend is ready, which is a surface
this plugin has an open reason to want, and it is a path a sibling could claim first.
Nothing else in the tree would have noticed it arriving.

`speaks-http-as-a-client.cs.fixture` is the near miss and it has to stay accepted.
It posts the extracted audio to an endpoint an operator configured, which is what the
remote backend does. Speaking HTTP and claiming a path on this server are opposite
directions, and a vocabulary as coarse as the word http would refuse the file the
whole remote backend lives in.

`names-a-route-in-a-comment.cs.fixture` explains in its prose that this plugin
answers no path and claims none in its code. Read without the comment lines taken
out it would be the first thing refused, and the repair somebody would reach for is
deleting the explanation.

They carry `.cs.fixture` rather than a plain extension because two of them are
sources claiming things this plugin does not do, and a plain extension puts them in
front of the compiler and of anything that walks this tree for sources. The extension
is checked rather than trusted, in the leg that reads this directory.
