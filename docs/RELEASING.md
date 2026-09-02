# Releasing

A release is published by pushing a tag. Nothing is created by hand.

## The checklist

`docs/release-checklist.md` is the list of conditions a release is cut against,
one section per condition, each naming the command or the status check whose
verdict is the answer. It also says which conditions nothing answers yet, and
which issue owes each of those a route. Read it before the tag is pushed; nothing
in the publish run reads it for you.

## The tag

The tag has the form `X.Y.Z-stable` or `X.Y.Z.W-stable`, for example `1.4.0-stable`
or `0.1.0.0-stable`. The numeric part is the plugin version that Jellyfin installs,
and it must be exactly the `version` in `build.yaml`, written the same way, with the
same number of parts. The `-stable` suffix lives only in the tag and in the release
name.

## Cutting a release

1. Update `version` in `build.yaml` on the release branch and merge it.
2. Check that the commit you want to release is on that branch.
3. Push the tag for that commit:

    ```
    git tag 1.4.0-stable <commit>
    git push origin 1.4.0-stable
    ```

The `Publish Release` workflow takes it from there.

Push one tag at a time and wait for its run to finish. GitHub keeps at most one
queued run per concurrency group, and although the group here is keyed on the tag,
serialising them by hand is what keeps the release order readable.

## What the run produces

The workflow builds the plugin from the tagged commit, creates the GitHub release
for the tag, and attaches four files:

- the plugin archive
- the packaging metadata written beside it, `<archive>.zip.meta.json`
- one `.md5` file, the checksum of the archive
- one `.sha256` file for the same archive

The `.md5` is the value a Jellyfin catalog serves as the plugin checksum. There is
exactly one per release so that no generator can pair a checksum with the wrong
file. Both the archive and the metadata are checked for existence by name before the
release job runs, so a release with three of the four files is not a state this route
can reach.

The run also signs a build provenance statement for the archive, in a separate job
that downloads the archive and runs no build tooling. A downloaded archive can be
checked against it:

```
gh attestation verify <archive>.zip --repo <owner>/<repository>
```

Nothing here writes a plugin catalog. A GitHub release is the whole output. If this
repository previously published through the Jellyfin meta plugins workflow, that path
is gone and no catalog is fed until a manifest generator is added.

## What fails the run

- The tag does not end in `-stable`, or the workflow was started from something
  other than a tag.
- The numeric part of the tag differs from `version` in `build.yaml`.
- `build.yaml` is missing from the repository root. Every piece of plugin metadata
  the packaging step uses comes out of that file, so its absence is refused before
  any field is read.
- `build.yaml` is missing a required field, or `version`, `targetAbi`, `framework`
  or `guid` has the wrong shape.
- The plugin project declares neither `TargetFramework` nor `TargetFrameworks`, so
  there is nothing for `framework` to be compared against.
- `framework` in `build.yaml` names a target the plugin project is not built for.
- A packaging manifest that shadows `build.yaml` is present, such as `jprm.yaml` or
  `meta.yaml`.
- `build.yaml` declares an `image` file that is not in the repository.
- The tagged commit is not contained in a release branch, or the tag was moved after
  the run started.
- There is no `packages.lock.json` next to the plugin project, so the release build
  cannot restore against a reviewed dependency graph. One is committed, and the
  plugin project sets `RestorePackagesWithLockFile`, so an ordinary
  `dotnet restore` writes it. What this bullet is about is the file being removed
  or never regenerated after a dependency changed: a restore that is not in locked
  mode updates the file and says nothing, so a version raised without committing
  the result leaves every check on that change green and this step red at the tag.
- The version stamped into the assembly is not the version in `build.yaml`.
- The build produced no archive, or more than one, or no packaging metadata.
- A release already exists for the tag.
- Asking whether a release exists for the tag comes back as neither a yes nor a
  no. An unclear answer is not published on.

All of these fail before anything is published.

This list is kept by hand and nothing compares it against
`.github/workflows/publish.yaml`, so a refusal added to the run reaches this page
only if somebody writes it here as well. Three of the entries above arrived that
way, found by reading the two side by side rather than by anything that runs, and
the same reading is what would find the next one. No issue names a mechanism for
it, and this sentence is the whole of the disclosure.

## What the run notes without failing

The packaging tool warns when `build.yaml` declares neither `image` nor `imageUrl`.
The plugin then shows without a logo in a catalog. That is a warning on every run
until a logo exists, and it is not a reason to hold a release.

## Re-running

A release that exists is not touched again. The release job asks whether a release
exists for the tag before it writes anything and stops if one does, and the upload
step is configured not to replace an asset of the same name. Replacing the bytes of a
version people have already installed is the failure this prevents, and it is worth
more than the convenience of a re-run.

So: if a release went out with the wrong contents, fix the problem, raise the version
in `build.yaml`, and push a new tag.

If a run failed **before** the release was created, the tag is still clean. Fix the
cause and re-run the workflow from the Actions page, or delete and re-push the tag.

If a run failed **after** the release was created but before every asset was attached,
the release is incomplete and a re-run will refuse it. What is possible then depends
on the repository settings below. Without immutable releases you can delete the
incomplete release, delete the tag, and push it again. With immutable releases you
cannot, and the version has to be raised.

## Repository settings this expects

- Default workflow permissions set to read only.
- A rule that restricts who may push `*-stable` tags.
- Which branches a release may be cut from, and therefore which branches the tag rule
  above and the required checks below have to cover.
  The release branches are `master`.
  That list is not decided here. It is `RELEASE_REFS` in
  `.github/workflows/publish.yaml`, which the publish run reads to refuse a tag on a
  commit no release branch contains, and a page carrying the phrase instead of the
  names reads as correct against every list there could ever be. The two are compared,
  so a branch added there and not named here is a red suite rather than a gate quietly
  covering less than the release route allows. Whether the gate is required on any of
  them is a repository setting, read by nothing in this tree, and #54 is where that
  comparison is owed.
- Every context the ABI floor workflow reports, required on those branches. A
  ruleset entry names the context a run reports under rather than the workflow's
  title, so a name no job carries is a required check that never arrives and a
  branch that then stops merging.

  The contexts the ABI floor workflow reports are
  `The plugin compiles against the floor its manifest promises`, which is the build
  against the floor, and `And it refuses a symbol the floor does not have`, which is
  the half that refuses a symbol the floor does not carry.

  That list is compared against the jobs `.github/workflows/abi-floor.yml` declares,
  in both directions, by `AbiFloorContextsTests`. A job added there and not named
  here is a red suite rather than a check that runs on every pull request and is
  never required, and a name here that no job reports is a required context that
  never arrives. This bullet used to state how many there were and name them by
  hand, which reads as complete against a workflow of any size.
- Immutable releases, if the repository wants the guarantee that a published release
  can never be edited or deleted at all. The workflow does not depend on it: the
  refusal to touch an existing release is enforced in the release job. Turning it on
  removes the only recovery path for an incomplete release, so try it on one
  repository and cut a release there before turning it on everywhere.
