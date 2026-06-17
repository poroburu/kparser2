namespace kparser2.Decoders.Tests

open Xunit

/// Serializes tests that mutate the shared EntityRegistry singleton.
[<CollectionDefinition("EntityRegistry")>]
type EntityRegistryCollection = unit
