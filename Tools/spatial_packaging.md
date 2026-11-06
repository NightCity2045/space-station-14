# Fixed spatial qualification pair (G0)

Run only after tests and game processes from the checkout have finished. The
existing Content.Packaging tool deletes `bin`; packaging and tests must be
sequential. Both working trees must be clean and the engine must match the
committed gitlink. Publish the engine commit before changing the content gitlink.

With Python 3.11+ and SDK 10.0.100:

```sh
git submodule update --init --recursive
python3 Tools/package_spatial_pair.py --runtime linux-x64 --output release/qualification-pair
python3 Tools/verify_spatial_pair.py --pair release/qualification-pair --output release/qualification-install
```

Use `win-x64` on the Windows runner. An already built standalone engine ZIP can
be supplied with `--engine-client`; its commit, runtime, source cleanliness, file
set and every payload hash must match. Existing destination directories are
refused. Standard intermediate ZIPs in `release/` are regenerated; archive earlier
qualification evidence outside those paths before a new build.

Extract the client ZIP to a new directory. Start `start.sh` (Linux) or `start.cmd`
(Windows). For a local test, add `--username SpatialQualification --connect
--connect-address 127.0.0.1:1213`. The client contains the .NET runtime and native
package libraries. Linux still needs the normal desktop driver/system libraries.
Content verification remains enabled. No source checkout or developer-specific
absolute paths are needed to run the extracted client.

The verification command checks archive hashes, extracts both packages, starts
the loopback server and a headless client, and requires both a player attachment
on Packed with the lobby disabled and explicit sandbox verification in the client log. It records logs and results,
then stops its own processes. It does not certify graphical rendering or a full
round. Existing integration suites cover round and map/editor behavior; G1–G15
and the graphical/load scenarios require their own evidence.

The content packager runs from a temporary published copy. Content.Server has a
project reference to Content.Packaging, so running it directly from its build
directory causes recursive Rebuild to overwrite loaded DLLs and fail on Windows.

The server ZIP uses the existing headless packaging path and requires the .NET 10
runtime. Extract it separately and run `dotnet Robust.Server.dll` with the server's
configuration. For local qualification, bind network and status to loopback and
disable hub advertising. Do not expose a qualification server publicly.

The pair manifest records exact commits, upstream base, SDK, backend, runtime,
resource archive and binary archive hashes. Spatial schema versions, protocol
descriptor and its SHA-256 fingerprint must match the pinned engine source and
the embedded engine/pair manifests. A legacy artifact without this descriptor
is rejected before packaging or starting either executable. Run the manifest
checks with `python3 -m unittest discover -s Tools -p test_spatial_protocol_manifest.py`.
These are G0 artifacts, not proof of G1–G15,
spatial gameplay, supported-platform results or performance acceptance.
