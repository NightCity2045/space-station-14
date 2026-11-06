# Fixed spatial qualification pair (G0)

Run only after tests and game processes from the checkout have finished. The
existing Content.Packaging tool deletes `bin`; packaging and tests must be
sequential. Both working trees must be clean and the engine must match the
committed gitlink. Publish the engine commit before changing the content gitlink.

With Python 3.11+ and SDK 10.0.100:

```sh
git submodule update --init --recursive
python3 Tools/package_spatial_pair.py --runtime linux-x64 --output release/qualification-pair
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

The server ZIP uses the existing headless packaging path and requires the .NET 10
runtime. Extract it separately and run `dotnet Robust.Server.dll` with the server's
configuration. For local qualification, bind network and status to loopback and
disable hub advertising. Do not expose a qualification server publicly.

The pair manifest records exact commits, upstream base, SDK, backend, runtime,
resource archive and binary archive hashes. Spatial schema/protocol are null
until implemented and versioned. These are G0 artifacts, not proof of G1–G15,
spatial gameplay, supported-platform results or performance acceptance.
