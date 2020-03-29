# bunq-api-client-light
Easy to follow, lightweight C# bunq api client

The official Bunq SDK was a bit too heavy for my taste,
so I created a lightweight version in C# dotnet core 3.1 that 
does exactly what I needed (and nothing more 🙂).
The api client is easy to be injected into your own software.

The program creates a public private key pair, that you can store for later reference.

It fetches a sandbox apikey, which you can replace with your own api key, when use in production.

The api takes care of expiring sessions, too many requests (429) server responses and
validating the server responses with the server public key.

Currently only a very few endpoints are implemented (just what I needed),
but it is easily extendably with support for more endpoints.

// Ryan