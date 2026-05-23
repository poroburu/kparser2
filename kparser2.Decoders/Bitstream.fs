namespace kparser2.Decoders

open System

type BitstreamReader =
    { Data: byte[]
      ByteIndex: int
      BitIndex: int }

module BitstreamReader =
    let private fromPayload (data: byte[]) =
        if data.Length <= 4 then
            failwith "Packet too short for battle bitstream"
        else
            // Skip world header (4 bytes) and info-size byte (1 byte).
            { Data = data
              ByteIndex = 5
              BitIndex = 0 }

    let create (data: byte[]) = fromPayload data

    let private stepByte state =
        { state with
            ByteIndex = state.ByteIndex + 1
            BitIndex = 0 }

    let rec private readBit state =
        if state.ByteIndex >= state.Data.Length then
            failwith "Bitstream overrun"

        let value = (int state.Data.[state.ByteIndex] >>> state.BitIndex) &&& 1
        let bitIndex = state.BitIndex + 1

        let nextState =
            if bitIndex >= 8 then
                stepByte state
            else
                { state with BitIndex = bitIndex }

        value, nextState

    let readBits state bitCount =
        let rec loop state remaining acc shift =
            if remaining <= 0 then
                acc, state
            else
                let bit, next = readBit state
                loop next (remaining - 1) (acc ||| (uint32 bit <<< shift)) (shift + 1)

        loop state bitCount 0u 0

    let readBitFlag state =
        let value, next = readBits state 1
        (value = 1u, next)
