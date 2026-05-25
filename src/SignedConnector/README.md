# Signed Connector Sample

End-to-end walkthrough of the Surgewave plugin signing workflow:

1. **Publisher keygen** — one-time key pair per publisher identity.
2. **Build + sign** — `dotnet publish` packs and signs a `.swpkg` in a single step.
3. **Upload to marketplace** — multipart upload with the sidecar `.sig`.
4. **Install with `--require-signed`** — broker rejects unsigned or untrusted packages.

The connector itself (`HelloSourceConnector.cs`) is deliberately trivial — the value of this
sample is the workflow.

## 1. Generate a publisher key pair (once per publisher)

```bash
surgewave plugins keygen mycompany --output ./keys
```

Produces `./keys/mycompany.key` (private, keep secret) and `./keys/mycompany.pub` (public,
distribute to anyone who needs to trust your packages).

## 2. Build, pack, sign — one command

```bash
dotnet publish -c Release \
    -p:SurgewavePackPlugin=true \
    -p:SurgewaveSigningKey=./keys/mycompany.key
```

Produces:

- `artifacts/pub/Plugins/Kuestenlogik.Surgewave.Samples.SignedConnector-1.0.0.swpkg` — the package, with
  `plugin.json` + `lib/` + `deps/` + `sbom.json`.
- `.../Kuestenlogik.Surgewave.Samples.SignedConnector-1.0.0.swpkg.sig` — the detached ECDSA signature.

Verify locally:

```bash
surgewave plugins trust ./keys/mycompany.pub --plugins-dir ./plugins
surgewave plugins verify \
    artifacts/pub/Plugins/Kuestenlogik.Surgewave.Samples.SignedConnector-1.0.0.swpkg \
    --plugins-dir ./plugins
# Signature verified (signed by: mycompany)
```

## 3. Upload to the marketplace

Assuming the marketplace runs at `http://marketplace.example.com` with
`Surgewave:Marketplace:Signing:RequireSignedUploads=true`:

```bash
SPP=artifacts/pub/Plugins/Kuestenlogik.Surgewave.Samples.SignedConnector-1.0.0.swpkg
curl -X PUT http://marketplace.example.com/api/v1/packages \
    -F "file=@$SPP" \
    -F "signature=@$SPP.sig"
```

The marketplace verifies against its trust store (which must include `mycompany.pub`) and
records `IsSigned=true` + `SignerIdentity=mycompany` + `SignerProvider=builtin-ecdsa` on the
package metadata. The SBOM (`sbom.json`) gets extracted and served at
`/api/v1/packages/Kuestenlogik.Surgewave.Samples.SignedConnector/1.0.0/sbom`.

Downloads preserve the sidecar so consumers can re-verify locally.

## 4. Install on the broker with strict verification

Add to the broker's `appsettings.json`:

```json
{
  "Surgewave": {
    "Plugins": {
      "Signer": {
        "Name": "builtin-ecdsa",
        "RequireSignedPackages": true
      }
    }
  }
}
```

And place `mycompany.pub` in `./plugins/trusted-keys/` (or use `surgewave plugins trust`). Then
install — the broker verifies the signature before unpacking:

```bash
surgewave plugins install Kuestenlogik.Surgewave.Samples.SignedConnector \
    --from-source marketplace \
    --require-signed
```

An unsigned package, a tampered archive, or a signature from a publisher not in
`plugins/trusted-keys/` aborts the install with a clear error.

## Related docs

- [Plugin Signing](../../../Surgewave/docs/security/plugin-signing.md) — full reference, including
  the `charter` enterprise provider and custom `IPluginPackageSignerProvider` implementations.
