# kparser2
Generate a diagram describing the following ETL workflow for a MMORPG parser. 

## Game client.
Final Fantasy is a MMORPG with the Ashita addon system.
The addon will be written in Lua.
I have an addon called `kpacket` that will publish game packets over ZMQ.

## Desktop client
The desktop client will be written in F#
The desktop client `kparser2` will subscribe to those game packets over ZMQ
The desktop will extract the packets by parsing them into F# types.
The desktop will provide a plugin system for the transform and load feature. This will allow user to write scripts to transform the game logic from packets to a database, then that database can be queried to load analytics to a UI pane that can be loaded in tabs or cards to display the output of many plugins at one time.

## notes
There are two zones, the game and desktop client. These will typically be on the same computer. But you could imagine a scenario where a user is playing on a Steam Deck and could parse from their laptop.
