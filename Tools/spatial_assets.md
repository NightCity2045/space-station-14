# B01 authored profiles and asset inventory

The shared spatial prototype types hold content decisions. They do not store a
second transform or perform engine rendering/physics:

- `spatialPresentation`: explicit category, physical dimensions, pivot, permitted
  billboard tilt, declared gameplay states and review notes. Existing Sprite/RSI
  layers remain the image source. `SpatialPresentation` binds an entity prototype
  to this profile through ordinary inheritance and component replication.
- `spatialGeometry`: boundary, slab or legacy full-cell block dimensions, material
  name and real openings. CreateBoundary/CreateSlab return engine semantic records
  with caller-supplied canonical frame/layer/surface/entity/revision references.
  Legacy blocks preserve their footprint; no edge relocation is inferred.
- `spatialBarrier`: independent movement, view and projectile permissions,
  normalized gas conductance and sound transmission. Shader alpha never assigns
  these values. Gameplay adapters must select profiles from existing authoritative
  door/window state and commit geometry/topology together.
- `spatialMaterial`: existing static PNG binding, linear color/emission, metre UV
  repeat, alpha mode/cutoff and sampling. SpatialMaterialBinding translates these
  explicitly to engine render data and refuses an unresolved authored texture.
  PixelMip resources must finish isolated engine uploads before binding; loading,
  ownership and complete baseline material assignments remain integration work.

Dimensions and state coverage must be authored for the actual object. There is no
automatic blanket assignment to existing mechanical prototypes. A decorative
fallback cannot replace structural/body presentation. Geometry references a named
material; selecting actual textures and rendering bindings remains client content
work. Decorative tiles do not create structural support or airtightness.

## Resolved catalog

Run `spatial_asset_catalog` in the content client. It writes
`spatial-asset-catalog.yml` to the client's user-data directory. The integration
test `SpatialAssetCatalogTest` exports the same report as a test attachment.

The catalog reads the engine's resolved entity/tile prototype mappings, including
inherited components. It does not implement a separate YAML inheritance resolver
or spawn every prototype. It records component names, explicit presentation
bindings, declared supported states, RSI/PNG references and resource errors.
Distinct RSI paths are inspected once; reports include image dimensions, every
available state, existing 1/4/8 directions, animation delays and image existence.
Referenced images are never manufactured to fill missing artistic directions.

Resource references are collected from resolved scalar values ending in `.rsi` or
`.png`. This also exposes art referenced by equipment and appearance components;
runtime-generated paths and state-selection logic still need review. Missing
bindings and invalid references stay visible. Existing tile subfloor/base-turf
metadata is recorded as legacy metadata, not an inferred structural profile.

Every entry remains `unreviewed`, including one with a valid explicit binding.
Binding presence is not proof of complete states, body correspondence, equipment,
lying/seated/dead appearance, item presentation or runtime effects. The mechanical
component check catches known dangerous decorative fallbacks; it is not an
exhaustive classification of every gameplay system. B01 acceptance still requires
reviewing and covering every applicable baseline object and its state adapters.

The initial profile tests use authored test dimensions only. They check loading,
permeability independence, invalid dimensions/fallbacks, preservation of legacy
cell volume and actual window/deck voids in engine solid parts. They do not claim
that vanilla objects have already been migrated.
