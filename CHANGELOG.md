## Implemented functionality
The project implements the IPK24-CHAT protocol. The protocol is divided into two variants: UDP and TCP, which are implemented in separate namespaces. Proper error handling is implemented, and the server can handle all of the needed commands and messages from numerous clients. The server can also handle the Ctrl+C signal to close the application by sending a `BYE` messages to all connected clients.

## Known limitations
I'm not aware of any limitations at the moment.