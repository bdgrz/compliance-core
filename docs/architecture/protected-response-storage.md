# Protected response content storage

`AesProtectedResponseContentStore` persists encrypted original response bodies through `IImmutableContentStore`. It never generates a host-local or process-lifetime encryption key.

Configure `ProtectedResponses:ActiveKeyVersionId` and base64 AES keys under `ProtectedResponses:Keys:<version>`. Every decoded key is exactly 32 bytes. Equivalent environment variable names use double underscores, for example `ProtectedResponses__ActiveKeyVersionId` and `ProtectedResponses__Keys__version_1`. API and worker hosts must receive the same configured persistent key ring. Key values belong in the existing secret configuration source, never source control or logs.

Missing configuration fails closed when a new protected result is requested. Invalid key encoding, invalid key version identifiers, a missing configured active version or a key of the wrong size fails composition. Rotation selects a new active version while retaining all older versions needed by supported original-result replay. Restarting a host with the retained ring can read earlier ciphertext. Removing a needed decryption key leaves that result unavailable; it never mints a replacement capability. Disposal must follow the selected fail-closed replay-retention policy.

The version-one envelope contains a fixed versioned magic header, length-prefixed content identity/key version/result schema, a fresh 96-bit nonce, ciphertext and a 128-bit GCM tag. Its SHA-256 and byte length cover the complete envelope. Additional authenticated data includes the header, realm, all affected-owner address components, root operation UUID and original actor kind/identity. Display text is not actor identity. Authentication refuses swaps across actor, owner, realm, operation, schema, object identity or key version even if an adapter incorrectly returns bytes from another scope.

The store bounds plaintext to one MiB, verifies publication metadata, reads the envelope back, authenticates it and compares the round-trip plaintext before publishing a reference. No raw ticket/body is placed in the immutable storage adapter or an exception message. Read operations verify envelope metadata and digest again at the encryption boundary.

This storage subset does not enable the protected artifact download API. The selected admission/grant protocol remains separate until its independent first-owner collision and source-settlement recovery gate is resolved.
