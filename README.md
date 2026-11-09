# Parada Certa — API Dispositivos (.NET)

Parte .NET do projeto **Parada Certa — Embarque Assistido**, desenvolvido na Global Solution 2026/2
(tema *CitizenTech*) da FIAP, turma 2TDS.

> **Em uma frase:** o totem instalado na parada é o jeito de o cidadão pedir ajuda para embarcar sem precisar de
> celular. Esta API garante que esse pedido chegue **uma única vez**, com segurança, mesmo com rede ruim
> ou com alguém apertando o botão sem parar.

---

## Sumário

1. [O problema e a solução](#1-o-problema-e-a-solução)
2. [O papel desta API no projeto](#2-o-papel-desta-api-no-projeto)
3. [Como funciona](#3-como-funciona)
4. [Tecnologias](#4-tecnologias)
5. [Critérios da disciplina e onde estão no código](#5-critérios-da-disciplina-e-onde-estão-no-código)
6. [Pré-requisitos](#6-pré-requisitos)
7. [Como rodar (passo a passo)](#7-como-rodar-passo-a-passo)
8. [Como testar a API](#8-como-testar-a-api)
9. [Testes automatizados](#9-testes-automatizados)
10. [Endpoints](#10-endpoints)
11. [Combinados com o restante do grupo](#11-combinados-com-o-restante-do-grupo)
12. [Configuração e variáveis de ambiente](#12-configuração-e-variáveis-de-ambiente)
13. [Deploy](#13-deploy)
14. [Estrutura do projeto](#14-estrutura-do-projeto)
15. [Decisões de arquitetura](#15-decisões-de-arquitetura)
16. [Problemas comuns](#16-problemas-comuns)
17. [O que ainda precisa ser feito](#17-o-que-ainda-precisa-ser-feito)

---

## 1. O problema e a solução

Pessoas com deficiência visual, idosos e pessoas com mobilidade reduzida têm dificuldade para identificar o ônibus
certo e sinalizar que querem embarcar. Muitas vezes o motorista passa sem perceber que alguém precisava de ajuda.

O **Parada Certa** coloca um **totem (ESP32)** na parada. O passageiro **aproxima o cartão** (Bilhete Único) ou
**aperta um botão**. O sistema cria uma **solicitação de embarque**, e o motorista da linha chega sabendo que há
alguém esperando e que precisa de atenção.

## 2. O papel desta API no projeto

O projeto é dividido entre os integrantes do grupo. Esta API é **somente a parte .NET**.

| Parte | Responsabilidade |
| --- | --- |
| **Totem ESP32** (IoT) | Lê o cartão ou o botão e chama esta API |
| **API Dispositivos (.NET) — este repositório** | Porta de entrada dos totens: autentica, valida, evita duplicidade, grava e repassa o sinal |
| **API Java** | Consome a fila, cria a solicitação de embarque, cadastra passageiros, cartões, linhas e motoristas |
| **App Mobile** | Cuidador e motorista acompanham as solicitações |
| **Oracle** | Banco compartilhado (script em `db/scripts/01-ddl.sql`) |

**O que esta API faz:**
- autentica cada totem com JWT próprio (perfil `DISPOSITIVO`);
- recebe os sinais de cartão ou botão e garante que cada um seja processado **uma vez só** (idempotência);
- limita o volume de sinais por totem (rate limiting), contra botão travado ou abuso;
- grava o sinal no Oracle, publica na fila do RabbitMQ para a API Java e registra a telemetria no MongoDB;
- recebe o *heartbeat* (sinal de vida) dos totens;
- permite ao **gestor** cadastrar, ativar, desativar e acompanhar os totens.

**O que esta API não faz** (é feito por outras partes): criar a solicitação de embarque, avisar o motorista,
cadastrar passageiros, cartões, linhas e veículos, e as telas do app.

## 3. Como funciona

```
 Passageiro          Totem ESP32            API Dispositivos (.NET)             API Java
 aproxima cartão ──▶ POST /api/sinais ──▶ 1. JWT: é um totem válido?
 ou aperta botão     + Idempotency-Key    2. Rate limit: ≤ 10 sinais/min?
                                          3. Validação dos campos
                                          4. Essa chave já foi processada?
                                          5. O totem está ativo?
                                          6. Grava em SINAL_TOTEM (Oracle)
                                          7. Publica na fila sinal-totem ──────▶ cria a solicitação
                                          8. Registra evento (MongoDB)            de embarque
                                          ◀── 201 Created
```

1. **Login do totem.** Ao ligar, o totem envia o identificador e o segredo gravados no firmware para
   `POST /api/auth/dispositivo` e recebe um JWT válido por 24 h.
2. **Sinal.** O totem envia `POST /api/sinais` com o tipo (`CARTAO` ou `BOTAO`), o UID do cartão (se houver),
   a data e hora, e o cabeçalho `Idempotency-Key`, uma chave única por acionamento.
3. **Idempotência.** O Wi-Fi da parada é instável e o firmware reenvia quando não recebe resposta.
   - Chave nova: grava e responde **201**.
   - Mesma chave com o mesmo conteúdo: devolve o sinal já gravado com **200**, sem duplicar.
   - Mesma chave com conteúdo diferente: **409**.
4. **Privacidade do cartão.** O UID nunca é gravado puro. A API grava um HMAC-SHA256 do UID com uma chave
   secreta que a API Java também conhece. Assim a Java identifica o passageiro sem expor o UID.
5. **Repasse.** O sinal é gravado no Oracle, publicado na fila `sinal-totem` (RabbitMQ, com confirmação do broker)
   e registrado como evento no MongoDB.
6. **Heartbeat.** Periodicamente o totem chama `POST /api/heartbeats`. O gestor vê se o totem está online
   (heartbeat nos últimos 3 minutos).
7. **Gestão.** O gestor faz login em `POST /api/auth/gestor`. Com ele, cadastra totens (o segredo aparece uma
   única vez), ativa ou desativa, gera um segredo novo e consulta o estado e os últimos sinais de cada totem.

## 4. Tecnologias

| Item | Uso |
| --- | --- |
| **.NET 10 / ASP.NET Core Minimal API** | API REST |
| **Entity Framework Core + Oracle.EntityFrameworkCore** | Acesso ao Oracle (sem migrations; o banco vem do DDL do grupo) |
| **JWT Bearer** | Autenticação, com perfis `DISPOSITIVO` e `GESTOR` |
| **Rate Limiter nativo do ASP.NET Core** | Limite de requisições com resposta 429 |
| **Health Checks** | Verificação da API, Oracle, RabbitMQ e MongoDB |
| **OpenAPI + Swagger UI** | Documentação interativa em `/swagger` |
| **RabbitMQ.Client** | Publicação na fila `sinal-totem` |
| **MongoDB.Driver** | Telemetria dos totens (`eventos_totem`) |
| **BCrypt.Net-Next** | Senha do gestor (mesmo formato do Spring Security) |
| **xUnit + WebApplicationFactory** | Testes de unidade e integração |
| **Docker / Docker Compose** | Oracle, RabbitMQ e MongoDB locais, e imagem da API |

## 5. Critérios da disciplina e onde estão no código

| Critério | Pontos | Onde |
| --- | --- | --- |
| API RESTful, validações, códigos HTTP, OpenAPI/Swagger | 20 | `Endpoints/`, `Servicos/SinalValidator.cs`, `Servicos/LoginValidator.cs`, `/swagger` |
| Autenticação e autorização com JWT | 20 | `Seguranca/`, políticas `SomenteDispositivo` e `SomenteGestor` |
| Tratamento de requisições repetidas (idempotência) | 15 | `Servicos/SinalService.cs` + UNIQUE em `SINAL_TOTEM.CHAVE_IDEMPOTENCIA` |
| Rate limiting com 429 | 15 | `Configuracao/LimiteRequisicoes.cs` |
| Testes xUnit (unidade e integração) | 20 | `tests/ParadaCerta.Dispositivos.Tests` (65 testes) |
| Health checks da API, banco e serviços externos | 10 | `/health`, `/health/live`, `/health/ready` |

## 6. Pré-requisitos

| Ferramenta | Versão | Como verificar |
| --- | --- | --- |
| .NET SDK | 10.0 ou superior | `dotnet --version` |
| Docker Desktop | qualquer versão recente | `docker --version` e `docker compose version` |
| VSCode | qualquer versão recente | — |
| Git | qualquer versão recente | `git --version` |

Extensões do VSCode (o próprio VSCode sugere ao abrir a pasta, pelo `.vscode/extensions.json`):
**C# Dev Kit**, **C#**, **Container Tools**, **REST Client** e **EditorConfig**.

> Não é preciso instalar Oracle, RabbitMQ nem MongoDB: tudo roda em containers pelo `docker-compose.yml`.

## 7. Como rodar (passo a passo)

### 7.1 Clonar

```bash
git clone https://github.com/MaiconDouglas-dev/Ponto-certo.git
```

```bash
cd Ponto-certo
```

### 7.2 Criar os arquivos de configuração local

Os arquivos reais são ignorados pelo Git. Copie os exemplos (eles já vêm com valores para o ambiente local):

```bash
cp .env.example .env
```

```bash
cp src/ParadaCerta.Dispositivos.Api/appsettings.Development.example.json src/ParadaCerta.Dispositivos.Api/appsettings.Development.json
```

### 7.3 Subir as dependências (Oracle, RabbitMQ e MongoDB)

```bash
docker compose up -d
```

Na primeira vez, o Oracle leva alguns minutos. Acompanhe até os três serviços aparecerem como `healthy`:

```bash
docker compose ps
```

Na primeira subida, o container do Oracle executa automaticamente:
- `db/scripts/01-ddl.sql`: cria as tabelas do grupo;
- `db/scripts/02-seed-dev.sql`: cria dados de teste (uma parada, um totem e um gestor).

| Serviço | Endereço no seu computador |
| --- | --- |
| Oracle | `localhost:1521`, serviço `FREEPDB1` |
| RabbitMQ (AMQP) | `localhost:5672` |
| RabbitMQ (painel) | <http://localhost:15672> (usuário e senha do `.env`) |
| MongoDB | `localhost:27018` (27018 para não conflitar com um MongoDB instalado na máquina) |

### 7.4 Rodar a API

**Pelo VSCode:** pressione **F5** (configuração *API Dispositivos*). Ele compila, sobe a API e abre o Swagger.

**Pelo terminal:**

```bash
dotnet run --project src/ParadaCerta.Dispositivos.Api
```

A API fica em <http://localhost:5117>, e o Swagger em <http://localhost:5117/swagger>.

### 7.5 Conferir se está tudo de pé

```bash
curl http://localhost:5117/health
```

Os quatro itens (`api`, `oracle`, `rabbitmq`, `mongodb`) devem aparecer como `Healthy`.

### 7.6 Alternativa: API também em container

```bash
docker compose --profile api up -d --build
```

Nesse modo, a API fica em <http://localhost:8080>.

### 7.7 Desligar

```bash
docker compose down
```

Para **apagar os dados** e recriar o banco do zero na próxima subida:

```bash
docker compose down -v
```

### 7.8 Tarefas prontas no VSCode

Em *Terminal → Run Task*: `build`, `test`, `watch` (recompila ao salvar), `docker: subir dependências` e
`docker: derrubar dependências`.

## 8. Como testar a API

### 8.1 Dados do seed (somente ambiente local)

| Quem | Login | Senha / segredo |
| --- | --- | --- |
| Gestor | `gestor@paradacerta.dev` | `Gestor@123` |
| Totem | `TOTEM-0001` | `segredo-dev-totem-0001` |

### 8.2 Pelo arquivo `requisicoes.http` (recomendado)

Abra `requisicoes.http` no VSCode e clique em **Send Request** acima de cada bloco, na ordem. O token do login
é reaproveitado automaticamente nos blocos seguintes.

### 8.3 Pelo Swagger

1. Abra <http://localhost:5117/swagger>.
2. Execute `POST /api/auth/dispositivo` com `{"identificador":"TOTEM-0001","segredo":"segredo-dev-totem-0001"}`.
3. Copie o `accessToken`, clique em **Authorize** e cole o token (sem a palavra *Bearer*).
4. Execute `POST /api/sinais` informando um `Idempotency-Key` (ex.: `TOTEM-0001-000001`) e o corpo:
   `{"totemId":"TOTEM-0001","tipo":"CARTAO","uidCartao":"04A1B2C3D4","dataHora":"2026-11-18T14:32:05-03:00"}`.

### 8.4 Roteiro de demonstração

| Passo | O que fazer | Resultado esperado |
| --- | --- | --- |
| 1 | Login do totem | 200 com `accessToken` |
| 2 | Enviar um sinal com chave nova | **201** + `Location` |
| 3 | Reenviar a **mesma** requisição | **200** + cabeçalho `Idempotency-Replayed: true` (sem duplicar) |
| 4 | Mesma chave, trocando `CARTAO` por `BOTAO` | **409** |
| 5 | Enviar 11 sinais seguidos com chaves diferentes | o 11º recebe **429** + `Retry-After` |
| 6 | Chamar `/api/sinais` sem token | **401** |
| 7 | Login do gestor e `GET /api/totens/1` | totem com `online: true` depois de um heartbeat |
| 8 | `GET /health` | API, Oracle, RabbitMQ e MongoDB `Healthy` |
| 9 | Painel do RabbitMQ, fila `sinal-totem` | uma mensagem por sinal novo |

## 9. Testes automatizados

```bash
dotnet test
```

- **65 testes** de unidade e de integração.
- **Não precisam de Docker:** o Oracle é trocado por SQLite em memória (que respeita o índice UNIQUE da
  idempotência), e o RabbitMQ e o MongoDB por implementações falsas.
- Autenticação, autorização, rate limiting, validação e serialização são **os reais** da aplicação.

| Pasta | Cobre |
| --- | --- |
| `Unidade/` | Validação do sinal, hash do cartão (valor de referência calculado fora do .NET), JWT, regras do `SinalService`, classificação de falha de dependência |
| `Integracao/` | API completa: 201, 200 repetido, 400, 401, 403, 409, 429, 503, fluxo do gestor, health checks, documento OpenAPI, datas em UTC e proxy reverso |

## 10. Endpoints

| Método | Rota | Perfil | Descrição |
| --- | --- | --- | --- |
| POST | `/api/auth/dispositivo` | — | Identificador + segredo do totem → JWT `DISPOSITIVO` (24 h) |
| POST | `/api/auth/gestor` | — | E-mail + senha (tabela `USUARIO`, BCrypt) → JWT `GESTOR` (2 h) |
| POST | `/api/sinais` | DISPOSITIVO | Recebe sinal de cartão ou botão |
| POST | `/api/heartbeats` | DISPOSITIVO | Sinal de vida (só MongoDB) |
| GET | `/api/sinais/{id}` | GESTOR | Consulta um sinal |
| GET | `/api/totens?status=` | GESTOR | Lista totens (filtro `ATIVO` ou `INATIVO`) |
| GET | `/api/totens/{id}` | GESTOR | Estado: último sinal, último heartbeat e se está online |
| POST | `/api/totens` | GESTOR | Cadastra totem e devolve o segredo **uma única vez** |
| PATCH | `/api/totens/{id}/status` | GESTOR | `ATIVO` ou `INATIVO` |
| POST | `/api/totens/{id}/segredo` | GESTOR | Gera novo segredo |
| GET | `/api/totens/{id}/sinais?limite=` | GESTOR | Últimos sinais do totem (1 a 100, padrão 20) |
| GET | `/health`, `/health/live`, `/health/ready` | — | Health checks |

### 10.1 POST /api/sinais em detalhe

```http
POST /api/sinais
Authorization: Bearer <token do dispositivo>
Idempotency-Key: TOTEM-0001-000123
Content-Type: application/json

{"totemId":"TOTEM-0001","tipo":"CARTAO","uidCartao":"04A1B2C3D4","dataHora":"2026-11-18T14:32:05-03:00"}
```

| Campo | Regra |
| --- | --- |
| `Idempotency-Key` (cabeçalho) | Obrigatório; até 80 caracteres: letras, números, `-`, `_`, `.` ou `:` |
| `totemId` | Obrigatório; igual ao totem do token |
| `tipo` | `CARTAO` ou `BOTAO` |
| `uidCartao` | Obrigatório se `CARTAO` (4 a 10 bytes em hexadecimal); proibido se `BOTAO` |
| `dataHora` | Obrigatória, ISO 8601 com fuso |

| Situação | Resposta |
| --- | --- |
| Chave nova | **201** + `Location: /api/sinais/{id}` |
| Mesma chave, mesmo conteúdo (reenvio do firmware) | **200** com o mesmo sinal + `Idempotency-Replayed: true` |
| Mesma chave, conteúdo diferente | **409** |
| Sem `Idempotency-Key`, tipo inválido, UID inválido | **400** (Problem Details com erros por campo) |
| Sem token ou token inválido | **401** |
| Token de outro totem, de gestor, ou totem inativo | **403** |
| Mais de 10 sinais no minuto pelo mesmo totem | **429** + `Retry-After` |
| RabbitMQ fora do ar | **503**: nada fica gravado; o totem reenvia com a mesma chave |
| Oracle ou outra dependência fora do ar (em qualquer rota) | **503** + `Retry-After: 15` |

### 10.2 Regras gerais

- Todos os erros seguem o padrão **Problem Details** (RFC 9457), com `application/problem+json`.
- Datas de resposta (`recebidoEm`, `criadoEm`...) saem sempre em **UTC**, com `Z` no final.
- Login com campo vazio responde **400**; credencial errada, **401**.

### 10.3 Limites de requisição

| Política | Limite | Contado por |
| --- | --- | --- |
| Sinais | 10 por minuto | totem |
| Heartbeat | 6 por minuto | totem |
| Login | 5 por minuto | IP do cliente |

Os valores ficam em `appsettings.json` (seção `LimiteRequisicoes`) e podem ser trocados por variável de ambiente.

## 11. Combinados com o restante do grupo

### 11.1 Hash do cartão (a API Java precisa calcular igual)

```
uid_hash = hex_minúsculo( HMAC-SHA256( chave = Cartao:ChaveHmac, mensagem = UTF-8(UID normalizado) ) )
```

- UID normalizado: sem espaços, `:` e `-`, em MAIÚSCULAS (`04:a1:b2:c3:d4` → `04A1B2C3D4`).
- Valor de referência: chave `chave-hmac-de-teste-com-mais-de-32-caracteres` + UID `04A1B2C3D4` →
  `f2eb07590200c3b0cc629c26b51b3f681d1203a0edd18d675800e0b9c065e2c9`.
- As duas APIs precisam usar **a mesma chave HMAC** em cada ambiente.

### 11.2 Mensagem na fila `sinal-totem`

Fila durável e sem argumentos extras. A mensagem é persistente, com `content-type: application/json` e
`message-id` igual à Idempotency-Key.

```json
{
  "sinalId": 42,
  "chaveIdempotencia": "TOTEM-0001-000123",
  "totemId": 1,
  "totemIdentificador": "TOTEM-0001",
  "paradaId": 1,
  "tipo": "CARTAO",
  "uidHash": "f2eb0759...e2c9",
  "dataHoraTotem": "2026-11-18T14:32:05-03:00",
  "recebidoEm": "2026-11-18T17:32:05.123Z"
}
```

A Java usa `chaveIdempotencia` como `SOLICITACAO_EMBARQUE.CHAVE_IDEMPOTENCIA` e, depois de criar a solicitação,
preenche `SINAL_TOTEM.SOLICITACAO_ID`. Datas gravadas pela .NET estão em **UTC**.

### 11.3 Banco de dados

As tabelas vêm do script do grupo (`db/scripts/01-ddl.sql`). Esta API usa:

| Tabela | Acesso |
| --- | --- |
| `TOTEM` | leitura e escrita |
| `SINAL_TOTEM` | leitura e escrita |
| `PARADA` | somente leitura |
| `USUARIO` | somente leitura (login do gestor) |

Não há migrations do EF Core. Mudanças de tabela são feitas no DDL e combinadas com o grupo.

## 12. Configuração e variáveis de ambiente

Nada sensível fica versionado: `.env` e `appsettings.Development.json` são ignorados pelo Git.
Em produção, configure por variáveis de ambiente (o `__` separa as seções):

| Variável | Obrigatória | Exemplo / regra |
| --- | --- | --- |
| `ConnectionStrings__Oracle` | sim | `User Id=...;Password=...;Data Source=host:1521/servico` |
| `ConnectionStrings__MongoDb` | sim | `mongodb+srv://...` (Atlas) |
| `RabbitMq__Uri` | sim* | `amqps://usuario:senha@host/vhost` (*ou `HostName`, `Usuario`, `Senha`) |
| `Jwt__Chave` | sim | mínimo 32 caracteres; a API não sobe sem ela |
| `Cartao__ChaveHmac` | sim | mínimo 32 caracteres, **igual à da API Java** |
| `ProxyReverso__Habilitado` | não | `true` só atrás do proxy da nuvem (veja abaixo) |
| `LimiteRequisicoes__Sinais__Limite` | não | padrão `10` |

**Sobre `ProxyReverso__Habilitado`:** na nuvem, a API recebe as conexões por um proxy, então o IP visto é o do
proxy e todo mundo dividiria o mesmo limite de login. Com `true`, o IP do cliente passa a vir do
`X-Forwarded-For`. Deixe `false` (padrão) quando a API recebe conexões direto; senão o cliente poderia forjar o
próprio IP.

## 13. Deploy

A imagem Docker é multi-stage e roda sem usuário root, na porta 8080:

```bash
docker build -t parada-certa-dispositivos .
```

Passos para publicar (Azure App Service for Containers, Render ou similar):

1. Ter Oracle, RabbitMQ (ex.: CloudAMQP) e MongoDB (ex.: Atlas) acessíveis pela internet.
2. Rodar `db/scripts/01-ddl.sql` no Oracle. **Não** rode o `02-seed-dev.sql` em produção; crie o gestor pela
   API Java ou por um script próprio.
3. Publicar a imagem e configurar as variáveis da seção 12, com chaves novas (não as de desenvolvimento).
4. Ativar `ProxyReverso__Habilitado=true`.
5. Apontar o health check da plataforma para `/health/live` (vivo) e conferir `/health` depois do deploy.
6. Cadastrar os totens reais por `POST /api/totens` e gravar cada segredo no firmware.

> A API precisa **ficar no ar durante a correção** da Global Solution.

## 14. Estrutura do projeto

```
├── src/ParadaCerta.Dispositivos.Api/
│   ├── Program.cs              pipeline HTTP (ordem dos middlewares)
│   ├── Configuracao/           registro de serviços, rate limiting, OpenAPI, Problem Details, falha de dependência
│   ├── Contratos/              DTOs de entrada e saída
│   ├── Dados/                  DbContext (mapeamento para as tabelas Oracle)
│   ├── Dominio/                entidades Totem, SinalTotem, Parada, Usuario
│   ├── Endpoints/              rotas da Minimal API, agrupadas por assunto
│   ├── Eventos/                MongoDB (eventos_totem)
│   ├── Mensageria/             RabbitMQ (fila sinal-totem)
│   ├── Saude/                  health checks do RabbitMQ e do MongoDB
│   ├── Seguranca/              JWT, hash do cartão e do segredo do totem
│   └── Servicos/               regras de negócio e validações
├── tests/ParadaCerta.Dispositivos.Tests/
│   ├── Infra/                  WebApplicationFactory, SQLite em memória, fakes e dados de teste
│   ├── Unidade/
│   └── Integracao/
├── db/
│   ├── init/                   script que o container do Oracle roda na primeira subida
│   └── scripts/                DDL do grupo e seed de desenvolvimento
├── .vscode/                    depuração (F5), tarefas e extensões recomendadas
├── docker-compose.yml          Oracle, RabbitMQ, MongoDB (e a API, no perfil "api")
├── Dockerfile
└── requisicoes.http            requisições prontas para a extensão REST Client
```

## 15. Decisões de arquitetura

| Decisão | Por quê |
| --- | --- |
| **Um projeto só, com pastas por responsabilidade** | Para o tamanho desta API, separar em vários projetos (Clean Architecture completa) traria complexidade sem ganho. As dependências externas ficam atrás de interfaces (`IPublicadorSinais`, `IRegistroEventos`), o que já permite testar sem infraestrutura. |
| **Minimal API com `TypedResults`** | Os tipos de retorno documentam sozinhos as respostas no OpenAPI e são verificados pelo compilador. |
| **EF Core sem repositórios extras** | O `DbContext` já é unidade de trabalho e repositório; outra camada só repetiria código. |
| **Idempotência no serviço + UNIQUE no banco** | A consulta prévia resolve o caso comum; o UNIQUE é a última barreira quando dois envios chegam ao mesmo tempo. |
| **Publicação com confirmação do broker** | A API só responde 201 depois de o RabbitMQ confirmar que guardou a mensagem. Se falhar, desfaz o registro e responde 503 para o totem reenviar. |
| **MongoDB só para telemetria** | Se o MongoDB cair, o sinal continua valendo. Ele não é caminho crítico. |
| **UID do cartão em HMAC, segredo do totem em SHA-256** | Nada que identifique o passageiro ou dê acesso ao totem fica gravado em texto puro. |
| **Problem Details em todos os erros** | Formato padrão (RFC 9457), fácil de tratar no firmware, no app e na API Java. |

## 16. Problemas comuns

| Sintoma | Causa e solução |
| --- | --- |
| A API não sobe e reclama de `Jwt:Chave` ou `Cartao:ChaveHmac` | Falta o `appsettings.Development.json` (passo 7.2) ou a chave tem menos de 32 caracteres. |
| `/health` mostra `oracle` como `Unhealthy` | O Oracle ainda está iniciando. Espere o `docker compose ps` mostrar `healthy`. |
| Login ou sinal respondem **503** | Alguma dependência está fora do ar. Veja `/health` e `docker compose ps`. |
| MongoDB não conecta | Outro MongoDB está usando a porta 27017. O compose usa **27018** no host; confira a connection string. |
| O container do MongoDB não inicia em Linux com kernel 6.19+ | Incompatibilidade conhecida do `mongo:8.0`. O projeto usa `mongo:8.2`. |
| Erro de SQL ao criar as tabelas | No SQL*Plus, um comentário `--` logo depois de um `;` na mesma linha quebra o script. Deixe o comentário em uma linha própria e recrie com `docker compose down -v && docker compose up -d`. |
| **429** durante os testes manuais | O limite é por minuto; espere o tempo do cabeçalho `Retry-After`. |

## 17. O que ainda precisa ser feito

### 17.1 Entrega da Global Solution

- [ ] Criar o arquivo `.txt` com **RM, nome completo e turma** de cada integrante (sem ele, o desconto é de 100 pontos).
- [ ] Montar o `.zip` com o link deste repositório e o `.txt`.
- [ ] Publicar a API na nuvem (seção 13) e garantir que ela fique no ar durante a correção.
- [ ] Gravar ou ensaiar a demonstração seguindo o roteiro da seção 8.4.
- [ ] Preparar a explicação dos componentes .NET: pipeline de middlewares, injeção de dependência, EF Core, rate limiter, health checks e JWT.

### 17.2 Combinar com o grupo

- [ ] **Chave HMAC do cartão:** a mesma na API .NET e na API Java em cada ambiente; testar com o valor de referência da seção 11.1.
- [ ] **Contrato da fila `sinal-totem`:** validar com quem faz o consumidor Java (seção 11.2).
- [ ] **Firmware do totem:** gerar a `Idempotency-Key` uma vez por acionamento e reutilizá-la nos reenvios; tratar 401 (refazer login), 429 e 503 (esperar o `Retry-After`).
- [ ] **App Mobile:** definir se alguma tela usa esta API (ex.: gestão de totens pelo gestor).
- [ ] Corrigir no DDL original do grupo o comentário depois de `;` em `idx_tentativa_veic_res` (seção 16).

### 17.3 Melhorias técnicas planejadas

| Prioridade | Melhoria | Motivo |
| --- | --- | --- |
| Alta | **Transactional Outbox**: coluna `PUBLICADO_EM` em `SINAL_TOTEM` e um serviço em segundo plano que publica os pendentes | Hoje, se a API cair entre gravar no Oracle e publicar no RabbitMQ, o sinal fica gravado sem ir para a fila, e o reenvio responde 200 sem publicar. **Exige mudança no DDL do grupo.** |
| Alta | Idempotência por totem: `UNIQUE(totem_id, chave_idempotencia)` | Hoje a chave vale para todos os totens; chaves iguais de totens diferentes geram 409. **Exige mudança no DDL.** |
| Média | Conferir se o totem está ativo também no heartbeat e invalidar tokens ao trocar o segredo | Um totem desativado continua com token válido por até 24 h. |
| Média | Tempo de resposta igual no login do gestor, exista ou não o e-mail | Evita descobrir e-mails cadastrados pelo tempo de resposta. |
| Média | Índice e TTL na coleção `eventos_totem` | A consulta do último heartbeat fica mais lenta conforme a coleção cresce. |
| Média | Pipeline de CI (GitHub Actions) com `dotnet build` e `dotnet test` | Mostra a cada push que os testes passam. |
| Baixa | Reaproveitar o canal do RabbitMQ e declarar a fila uma vez só | Hoje cada publicação abre um canal e redeclara a fila. |
| Baixa | Testes de integração com Oracle real (Testcontainers) | Os testes atuais usam SQLite, que não reproduz todos os comportamentos do Oracle. |
| Baixa | Paginação em `GET /api/totens` e campo `versao` na mensagem da fila | Crescimento e evolução do contrato. |
