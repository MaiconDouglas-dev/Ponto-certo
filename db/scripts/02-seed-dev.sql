-- =====================================================================
-- PARADA CERTA | dados SÓ PARA DESENVOLVIMENTO LOCAL (docker-compose)
-- Nunca rode este script no banco da FIAP nem na nuvem.
--
-- Gestor:  gestor@paradacerta.dev / Gestor@123        (BCrypt, custo 10)
-- Totem:   TOTEM-0001 / segredo-dev-totem-0001        (SHA-256 em TOKEN_HASH)
-- =====================================================================

INSERT INTO usuario (nome, email, senha_hash, perfil)
VALUES ('Gestor Dev', 'gestor@paradacerta.dev',
        '$2a$10$QglNKFTueUl3jUtueFJx4O19L1upafVQytIWqwJjZwcJKdLdz/eLW', 'GESTOR');

INSERT INTO parada (codigo_sptrans, nome, endereco, latitude, longitude)
VALUES (340015329, 'Av. Paulista, 1578 - MASP', 'Av. Paulista, 1578 - Bela Vista, São Paulo', -23.561414, -46.655881);

INSERT INTO totem (parada_id, identificador, token_hash, status)
SELECT id, 'TOTEM-0001', 'b6ff3afcaf2fe27ae4e93c219e33a46678acdf787b7d8eea80d261970ab9af17', 'ATIVO'
FROM parada WHERE codigo_sptrans = 340015329;

COMMIT;
