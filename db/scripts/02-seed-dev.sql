-- =====================================================================
-- PARADA CERTA | dados SÓ PARA DESENVOLVIMENTO LOCAL (docker-compose)
-- Nunca rode este script no banco da FIAP nem na nuvem.
--
-- Gestor:  gestor@paradacerta.dev / Gestor@123        (BCrypt, custo 10)
-- Totem:   TOTEM-0001 / segredo-dev-totem-0001        (HMAC-SHA256 em TOKEN_HASH, chave de dev
--          dev-somente-local-hmac-cartao-0123456789abcdef: a mesma de Cartao:ChaveHmac / HASH_SECRET)
-- =====================================================================

INSERT INTO usuario (nome, email, senha_hash, perfil)
VALUES ('Gestor Dev', 'gestor@paradacerta.dev',
        '$2a$10$QglNKFTueUl3jUtueFJx4O19L1upafVQytIWqwJjZwcJKdLdz/eLW', 'GESTOR');

INSERT INTO parada (codigo_sptrans, nome, endereco, latitude, longitude)
VALUES (340015329, 'Av. Paulista, 1578 - MASP', 'Av. Paulista, 1578 - Bela Vista, São Paulo', -23.561414, -46.655881);

INSERT INTO totem (parada_id, nome_identificador, token_hash, status)
SELECT id, 'TOTEM-0001', '471be972f0b30f9f3d96920d31d0b7a8b7f94f267ec9e28a4d0cff0ba9be76b5', 'ATIVO'
FROM parada WHERE codigo_sptrans = 340015329;

COMMIT;
