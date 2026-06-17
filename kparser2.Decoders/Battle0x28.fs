namespace kparser2.Decoders

module Battle0x28 =
    let private readEffect (state: BitstreamReader) =
        let miss, state = BitstreamReader.readBits state 3
        let kind, state = BitstreamReader.readBits state 2
        let subKind, state = BitstreamReader.readBits state 12
        let _, state = BitstreamReader.readBits state 5
        let _, state = BitstreamReader.readBits state 5
        let value, state = BitstreamReader.readBits state 17
        let messageId, state = BitstreamReader.readBits state 10
        let _, state = BitstreamReader.readBits state 31

        let hasProc, state = BitstreamReader.readBitFlag state

        let procValue, state =
            if hasProc then
                let _, state = BitstreamReader.readBits state 6
                let _, state = BitstreamReader.readBits state 4
                let procValue, state = BitstreamReader.readBits state 17
                let _, state = BitstreamReader.readBits state 10
                int procValue, state
            else
                0, state

        let hasReact, state = BitstreamReader.readBitFlag state

        let reactValue, state =
            if hasReact then
                let _, state = BitstreamReader.readBits state 6
                let _, state = BitstreamReader.readBits state 4
                let reactValue, state = BitstreamReader.readBits state 14
                let _, state = BitstreamReader.readBits state 10
                int reactValue, state
            else
                0, state

        let effect =
            { Miss = int miss
              Kind = int kind
              SubKind = int subKind
              Param = int value
              MessageId = int messageId
              Value = int value
              HasProc = hasProc
              ProcValue = procValue
              HasReact = hasReact
              ReactValue = reactValue }

        effect, state

    let decode (data: byte[]) =
        try
            if data.Length < 8 then
                None
            else
                let mutable state = BitstreamReader.create data

                let actorId, next = BitstreamReader.readBits state 32
                state <- next
                let targetCount, next = BitstreamReader.readBits state 6
                state <- next
                let _, next = BitstreamReader.readBits state 4
                state <- next
                let commandNo, next = BitstreamReader.readBits state 4
                state <- next
                let commandArg, next = BitstreamReader.readBits state 32
                state <- next
                let info, next = BitstreamReader.readBits state 32
                state <- next

                let mutable targets = []

                if targetCount > 0u then
                    let count = min 64 (int targetCount)

                    for _ = 1 to count do
                        let targetId, next = BitstreamReader.readBits state 32
                        state <- next
                        let resultCount, next = BitstreamReader.readBits state 4
                        state <- next
                        let effectCount = min 8 (int resultCount)
                        let mutable effects = []

                        for _ = 1 to effectCount do
                            let effect, next = readEffect state
                            state <- next
                            effects <- effect :: effects

                        targets <-
                            { TargetId = targetId
                              Effects = List.rev effects }
                            :: targets

                Some
                    { ActorId = actorId
                      CommandNo = int commandNo
                      CommandArg = commandArg
                      Info = info
                      Targets = List.rev targets }
        with _ ->
            None
