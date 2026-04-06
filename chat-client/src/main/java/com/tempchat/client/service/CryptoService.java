package com.tempchat.client.service;

import javax.crypto.Cipher;
import javax.crypto.SecretKey;
import javax.crypto.SecretKeyFactory;
import javax.crypto.spec.GCMParameterSpec;
import javax.crypto.spec.PBEKeySpec;
import javax.crypto.spec.SecretKeySpec;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.security.SecureRandom;
import java.security.spec.KeySpec;
import java.util.Base64;

/**
 * AES-256-GCM encryption with PBKDF2 key derivation.
 *
 * Key  = PBKDF2WithHmacSHA256(password, salt=roomCode, 200_000 iters, 256 bits)
 * Wire = Base64( IV[12] || ciphertext+tag[n+16] )
 *
 * The server stores and relays Base64 ciphertext — it never sees plaintext.
 */
public class CryptoService {

    private static final int GCM_IV_BYTES   = 12;
    private static final int GCM_TAG_BITS   = 128;
    private static final int PBKDF2_ITERS   = 200_000;
    private static final int KEY_BITS       = 256;

    private final SecretKey key;
    private final SecureRandom rng = new SecureRandom();

    public CryptoService(String password, String roomCode) throws GeneralSecurityException {
        byte[] salt = roomCode.getBytes(StandardCharsets.UTF_8);
        SecretKeyFactory factory = SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256");
        KeySpec spec = new PBEKeySpec(password.toCharArray(), salt, PBKDF2_ITERS, KEY_BITS);
        byte[] raw = factory.generateSecret(spec).getEncoded();
        this.key = new SecretKeySpec(raw, "AES");
    }

    public String encrypt(String plaintext) throws GeneralSecurityException {
        byte[] iv = new byte[GCM_IV_BYTES];
        rng.nextBytes(iv);

        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        cipher.init(Cipher.ENCRYPT_MODE, key, new GCMParameterSpec(GCM_TAG_BITS, iv));
        byte[] ciphertext = cipher.doFinal(plaintext.getBytes(StandardCharsets.UTF_8));

        ByteBuffer buf = ByteBuffer.allocate(iv.length + ciphertext.length);
        buf.put(iv);
        buf.put(ciphertext);
        return Base64.getEncoder().encodeToString(buf.array());
    }

    /**
     * Returns decrypted plaintext, or null if decryption fails
     * (wrong password, corrupted data, or legacy unencrypted message).
     */
    public String decrypt(String encoded) {
        try {
            byte[] data = Base64.getDecoder().decode(encoded);
            if (data.length < GCM_IV_BYTES + 1) return null;

            ByteBuffer buf = ByteBuffer.wrap(data);
            byte[] iv = new byte[GCM_IV_BYTES];
            buf.get(iv);
            byte[] ciphertext = new byte[buf.remaining()];
            buf.get(ciphertext);

            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(GCM_TAG_BITS, iv));
            return new String(cipher.doFinal(ciphertext), StandardCharsets.UTF_8);
        } catch (Exception e) {
            return null;
        }
    }
}
