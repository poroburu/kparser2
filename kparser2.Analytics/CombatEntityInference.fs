namespace kparser2.Analytics

open kparser2.Decoders

module CombatEntityInference =
    let private isPet entityId =
        EntityRegistry.isLocalPet entityId

    let inferFromInteraction (interaction: Interaction) =
        let actorId = interaction.ActorId
        let targetId = interaction.TargetId

        let setKind entityId kind =
            EntityRegistry.setEntityKind entityId kind

        match interaction.InteractionType with
        | InteractionType.Harm ->
            if EntityRegistry.isLocalPlayer actorId && not (EntityRegistry.isLocalPlayer targetId) && not (isPet targetId) then
                setKind targetId EntityRegistry.EntityKind.Mob
            elif not (EntityRegistry.isLocalPlayer actorId) && EntityRegistry.isLocalPlayer targetId then
                setKind actorId EntityRegistry.EntityKind.Mob
            elif
                EntityRegistry.tryLocalPlayerId().IsSome
                && not (EntityRegistry.isLocalPlayer actorId)
                && not (EntityRegistry.isLocalPlayer targetId)
                && not (isPet actorId)
            then
                if isPet targetId then
                    setKind actorId EntityRegistry.EntityKind.Mob
                else
                    EntityRegistry.registerLocalPetActor actorId
                    setKind targetId EntityRegistry.EntityKind.Mob
        | InteractionType.Death ->
            if not (EntityRegistry.isLocalPlayer targetId) && not (isPet targetId) then
                setKind targetId EntityRegistry.EntityKind.Mob
        | _ -> ()
