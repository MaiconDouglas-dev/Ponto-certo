<div align="center">

# 🚏 Parada Certa — API Dispositivos

**Embarque assistido: o cidadão pede ajuda na parada, e o motorista chega sabendo.**

Parte .NET do projeto *Parada Certa*, da Global Solution 2026/2 (tema **CitizenTech**) · FIAP · 2TDS

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Minimal API](https://img.shields.io/badge/ASP.NET_Core-Minimal_API-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
[![Oracle](https://img.shields.io/badge/Oracle-EF_Core-F80000?logo=oracle&logoColor=white)](https://www.oracle.com/database/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-4.1-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![MongoDB](https://img.shields.io/badge/MongoDB-8.2-47A248?logo=mongodb&logoColor=white)](https://www.mongodb.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://docs.docker.com/compose/)
[![Testes](https://img.shields.io/badge/xUnit-66_testes-5C2D91)](#-testes-automatizados)

[Início rápido](#-início-rápido) ·
[Como funciona](#-como-funciona) ·
[Como testar](#-como-testar) ·
[Endpoints](#-endpoints) ·
[Integração com o grupo](#-integração-com-o-restante-do-grupo) ·
[Pendências](#-o-que-ainda-precisa-ser-feito)

</div>

---

> [!NOTE]
> **Em uma frase:** o totem na parada é o jeito de o cidadão pedir ajuda para embarcar **sem precisar de celular**.
> Esta API garante que o pedido chegue **uma única vez**, com segurança, mesmo com rede ruim ou com alguém
> apertando o botão sem parar.

## 📑 Sumário

- [Início rápido](#-início-rápido)
- [O problema e a solução](#-o-problema-e-a-solução)
- [O papel desta API no projeto](#-o-papel-desta-api-no-projeto)
- [Como funciona](#-como-funciona)
- [Critérios da disciplina](#-critérios-da-disciplina)
- [Tecnologias](#-tecnologias)
- [Como rodar](#-como-rodar)
- [Como testar](#-como-testar)
- [Testes automatizados](#-testes-automatizados)
- [Endpoints](#-endpoints)
- [Integração com o restante do grupo](#-integração-com-o-restante-do-grupo)
- [Configuração](#-configuração)
- [Deploy](#-deploy)
- [Estrutura do projeto](#-estrutura-do-projeto)
- [Decisões de arquitetura](#-decisões-de-arquitetura)
- [Problemas comuns](#-problemas-comuns)
- [O que ainda precisa ser feito](#-o-que-ainda-precisa-ser-feito)

---

## ⚡ Início rápido

Só precisa do **[Docker Desktop](https://www.docker.com/products/docker-desktop/)** aberto e do **Git**.
Não é preciso instalar .NET, Oracle, RabbitMQ nem MongoDB.

```bash
git clone https://github.com/MaiconDouglas-dev/Ponto-certo.git
```

```bash
cd Ponto-certo
```

```bash
docker compose up -d --build
```

Pronto. Na primeira vez, o download das imagens leva alguns minutos. Depois, abra:

| | Endereço |
| --- | --- |
| 📘 **Swagger** (documentação interativa) | <http://localhost:5117/swagger> |
| 💚 **Health check** | <http://localhost:5117/health> |
| 🐇 Painel do RabbitMQ | <http://localhost:15672> (usuário `parada`, senha `parada_dev`) |

> [!TIP]
> Para testar as rotas, use os [dados de teste](#dados-de-teste) e o [roteiro de demonstração](#roteiro-de-demonstração).

---

## 🎯 O problema e a solução

**O problema.** Pessoas com deficiência visual, idosos e pessoas com mobilidade reduzida têm dificuldade para
identificar o ônibus certo e sinalizar que querem embarcar. Muitas vezes o motorista passa sem perceber que
alguém precisava de ajuda.

**A solução.** O *Parada Certa* instala um **totem (ESP32)** na parada. O passageiro **aproxima o cartão**
(Bilhete Único) ou **aperta um botão**. O sistema cria uma **solicitação de embarque**, e o motorista da linha
chega sabendo que há alguém esperando e que precisa de atenção.

## 🧩 O papel desta API no projeto

Cada integrante do grupo cuida de uma parte. **Este repositório é somente a parte .NET.**

```mermaid
flowchart LR
    P([👤 Passageiro]) -->|cartão ou botão| T[📟 Totem ESP32]
    T -->|HTTP + JWT| API[API Dispositivos<br/>.NET 10]
    G([🧑‍💼 Gestor]) -->|JWT de gestor| API
    API -->|grava o sinal| ORA[(Oracle)]
    API -->|fila sinal-totem| RMQ[[RabbitMQ]]
    API -->|telemetria| MDB[(MongoDB)]
    RMQ --> JAVA[API Java]
    JAVA --> ORA
    JAVA --> APP[📱 App Mobile]

    classDef nossa fill:#512BD4,stroke:#3a1d9e,color:#fff
    class API nossa
```

| Parte | Responsabilidade |
| --- | --- |
| 📟 **Totem ESP32** (IoT) | Lê o cartão ou o botão e chama esta API |
| 🟪 **API Dispositivos (.NET)** · *este repositório* | Porta de entrada dos totens: autentica, valida, evita duplicidade, grava e repassa o sinal |
| ☕ **API Java** | Consome a fila, cria a solicitação de embarque e cadastra passageiros, cartões, linhas e motoristas |
| 📱 **App Mobile** | Cuidador e motorista acompanham as solicitações |
| 🗄️ **Oracle** | Banco compartilhado pelo grupo (script em [`db/scripts/01-ddl.sql`](db/scripts/01-ddl.sql)) |

<table>
<tr>
<th>✅ O que esta API faz</th>
<th>🚫 O que fica com outras partes</th>
</tr>
<tr>
<td valign="top">

- Autentica cada totem com JWT próprio (perfil `DISPOSITIVO`)
- Recebe sinais de cartão ou botão e processa cada um **uma vez só**
- Limita o volume de sinais por totem (rate limiting)
- Grava no Oracle, publica no RabbitMQ e registra telemetria no MongoDB
- Recebe o *heartbeat* (sinal de vida) dos totens
- Permite ao **gestor** cadastrar, ativar, desativar e acompanhar os totens

</td>
<td valign="top">

- Criar a solicitação de embarque
- Avisar o motorista
- Cadastrar passageiros, cartões, linhas e veículos
- Telas do aplicativo
- Firmware do totem

</td>
</tr>
</table>

## 🔄 Como funciona

```mermaid
sequenceDiagram
    autonumber
    actor P as Passageiro
    participant T as Totem ESP32
    participant A as API Dispositivos
    participant O as Oracle
    participant R as RabbitMQ
    participant M as MongoDB
    participant J as API Java

    P->>T: Aproxima o cartão ou aperta o botão
    T->>A: POST /api/sinais com JWT e Idempotency-Key
    A->>A: Confere JWT, limite de 10 por minuto e campos
    A->>O: A chave já foi usada? O totem está ativo?
    A->>O: Grava em SINAL_TOTEM
    A->>R: Publica na fila sinal-totem e espera a confirmação
    A->>M: Registra o evento SINAL_RECEBIDO
    A-->>T: 201 Created
    R->>J: Entrega a mensagem
    J->>J: Cria a solicitação de embarque
```

**Passo a passo:**

1. **Login do totem.** Ao ligar, o totem troca o identificador e o segredo gravados no firmware por um JWT
   válido por 24 h (`POST /api/auth/dispositivo`).
2. **Sinal.** A cada acionamento, o totem envia `POST /api/sinais` com o tipo (`CARTAO` ou `BOTAO`), o UID do
   cartão (se houver), a data e hora, e o cabeçalho `Idempotency-Key`, uma chave única por acionamento.
3. **Idempotência.** O Wi-Fi da parada é instável, e o firmware reenvia quando não recebe resposta:

   | Situação | Resposta | Efeito |
   | --- | --- | --- |
   | Chave nova | **201 Created** | Grava, publica e registra |
   | Mesma chave, mesmo conteúdo | **200 OK** + `Idempotency-Replayed: true` | Devolve o sinal já gravado, sem duplicar |
   | Mesma chave, conteúdo diferente | **409 Conflict** | Nada muda |

4. **Privacidade do cartão.** O UID nunca é gravado puro: a API grava um HMAC-SHA256 do UID com uma chave
   secreta que a API Java também conhece. Assim a Java identifica o passageiro sem expor o UID.
5. **Repasse confiável.** A API só responde 201 depois de o RabbitMQ **confirmar** que guardou a mensagem. Se a
   fila estiver fora do ar, a API desfaz a gravação e responde 503, para o totem reenviar com a mesma chave.
6. **Heartbeat.** O totem chama `POST /api/heartbeats` periodicamente. Ele aparece como *online* se o último
   heartbeat tiver menos de 3 minutos.
7. **Gestão.** O gestor faz login em `POST /api/auth/gestor` e, com o token, cadastra totens (o segredo aparece
   **uma única vez**), ativa ou desativa, gera um segredo novo e consulta o estado e os últimos sinais de cada totem.

## 🏆 Critérios da disciplina

| Critério | Pontos | Onde está |
| --- | :---: | --- |
| API RESTful, validações, códigos HTTP e OpenAPI/Swagger | 20 | [`Endpoints/`](src/ParadaCerta.Dispositivos.Api/Endpoints), [`SinalValidator.cs`](src/ParadaCerta.Dispositivos.Api/Servicos/SinalValidator.cs), [`LoginValidator.cs`](src/ParadaCerta.Dispositivos.Api/Servicos/LoginValidator.cs), `/swagger` |
| Autenticação e autorização com JWT | 20 | [`Seguranca/`](src/ParadaCerta.Dispositivos.Api/Seguranca), políticas `SomenteDispositivo` e `SomenteGestor` |
| Tratamento de requisições repetidas (idempotência) | 15 | [`SinalService.cs`](src/ParadaCerta.Dispositivos.Api/Servicos/SinalService.cs) + UNIQUE em `SINAL_TOTEM.CHAVE_IDEMPOTENCIA` |
| Rate limiting com resposta 429 | 15 | [`LimiteRequisicoes.cs`](src/ParadaCerta.Dispositivos.Api/Configuracao/LimiteRequisicoes.cs) |
| Testes xUnit de unidade e integração | 20 | [`tests/`](tests/ParadaCerta.Dispositivos.Tests) · 66 testes |
| Health checks da API, do banco e dos serviços externos | 10 | `/health`, `/health/live`, `/health/ready` |

## 🧰 Tecnologias

| Tecnologia | Para quê |
| --- | --- |
| **.NET 10 · ASP.NET Core Minimal API** | API REST |
| **Entity Framework Core + Oracle.EntityFrameworkCore** | Acesso ao Oracle (sem migrations: o banco vem do DDL do grupo) |
| **JWT Bearer** | Autenticação com perfis `DISPOSITIVO` e `GESTOR` |
| **Rate Limiter nativo do ASP.NET Core** | Limite de requisições com resposta 429 |
| **Health Checks** | Saúde da API, Oracle, RabbitMQ e MongoDB |
| **OpenAPI + Swagger UI** | Documentação interativa |
| **RabbitMQ.Client** | Publicação na fila `sinal-totem`, com confirmação do broker |
| **MongoDB.Driver** | Telemetria dos totens (coleção `eventos_totem`) |
| **BCrypt.Net-Next** | Senha do gestor, no mesmo formato do Spring Security |
| **xUnit + WebApplicationFactory** | Testes de unidade e integração |
| **Docker · Docker Compose** | Ambiente completo com um comando |

---

## 🚀 Como rodar

### Requisitos

Há dois jeitos de rodar:

| Jeito | Precisa de | Indicado para |
| --- | --- | --- |
| **A · Só Docker** | Docker Desktop (ou Docker Engine no Linux) e Git | Avaliar, demonstrar, testar |
| **B · Desenvolvimento** | Docker + .NET SDK 10 + VSCode | Alterar o código |

| Item | Mínimo |
| --- | --- |
| Sistema | Windows 10/11 (Docker Desktop com WSL 2), macOS (Intel ou Apple Silicon) ou Linux (x64 ou ARM64) |
| Memória | 8 GB de RAM (os containers usam cerca de 3 GB; o Oracle é o maior) |
| Disco | cerca de 5 GB livres para as imagens |
| Portas livres | `5117` API · `1521` Oracle · `5672` e `15672` RabbitMQ · `27018` MongoDB (todas podem ser trocadas) |

Para conferir se o Docker está funcionando:

```bash
docker compose version
```

### A · Só Docker (recomendado)

**1. Subir tudo**, com o Docker Desktop aberto, na pasta do projeto:

```bash
docker compose up -d --build
```

Sem nenhuma configuração extra, esse comando:

1. baixa as imagens do Oracle, RabbitMQ e MongoDB (só na primeira vez);
2. compila a API dentro de um container;
3. cria as tabelas ([`01-ddl.sql`](db/scripts/01-ddl.sql)) e os dados de teste ([`02-seed-dev.sql`](db/scripts/02-seed-dev.sql));
4. só inicia a API depois que Oracle, RabbitMQ e MongoDB estão prontos.

> [!NOTE]
> Não é preciso criar o arquivo `.env`: o `docker-compose.yml` já tem valores padrão para o ambiente local.
> Copie `.env.example` para `.env` só se quiser trocar uma senha ou uma porta.

**2. Acompanhar** até o Oracle, o RabbitMQ e o MongoDB aparecerem como `healthy`:

```bash
docker compose ps
```

**3. Conferir:** abra <http://localhost:5117/health>. O `status` geral e os quatro itens (`api`, `oracle`,
`rabbitmq`, `mongodb`) devem estar `Healthy`.

| Serviço | Endereço | Acesso |
| --- | --- | --- |
| API e Swagger | <http://localhost:5117/swagger> | veja os [dados de teste](#dados-de-teste) |
| Oracle | `localhost:1521`, serviço `FREEPDB1` | usuário `parada`, senha `parada_dev` |
| RabbitMQ | `localhost:5672` · painel <http://localhost:15672> | usuário `parada`, senha `parada_dev` |
| MongoDB | `mongodb://parada:parada_dev@localhost:27018/?authSource=admin` | — |

<details>
<summary><b>Comandos do dia a dia</b></summary>

Ver os logs da API:

```bash
docker compose logs -f api
```

Desligar (os dados ficam guardados):

```bash
docker compose down
```

Desligar **apagando os dados** (o banco é recriado do zero na próxima subida):

```bash
docker compose down -v
```

Atualizar depois de um `git pull`:

```bash
docker compose up -d --build
```

</details>

### B · Desenvolvimento (VSCode)

A API roda fora do container, e só as dependências ficam no Docker.

**1.** Instale o **.NET SDK 10** e o **VSCode**. Ao abrir a pasta, aceite as extensões recomendadas
(C# Dev Kit, C#, Container Tools, REST Client e EditorConfig). Confira o SDK:

```bash
dotnet --version
```

**2.** Crie a configuração local da API (o arquivo real é ignorado pelo Git):

```bash
cp src/ParadaCerta.Dispositivos.Api/appsettings.Development.example.json src/ParadaCerta.Dispositivos.Api/appsettings.Development.json
```

**3.** Se a API em container estiver rodando, pare-a para liberar a porta 5117:

```bash
docker compose stop api
```

**4.** Suba só as dependências:

```bash
docker compose up -d oracle rabbitmq mongodb
```

**5.** Pressione **F5** no VSCode (configuração *API Dispositivos*) ou rode:

```bash
dotnet run --project src/ParadaCerta.Dispositivos.Api
```

> [!TIP]
> A API fica no mesmo endereço nos dois modos (<http://localhost:5117>), então o `requisicoes.http` e o Swagger
> funcionam igual. Em *Terminal → Run Task* há tarefas prontas: `build`, `test`, `watch`,
> `docker: subir dependências`, `docker: subir tudo (com a API)` e `docker: derrubar tudo`.

---

## 🧪 Como testar

### Dados de teste

Criados pelo [`02-seed-dev.sql`](db/scripts/02-seed-dev.sql), **somente para o ambiente local**:

| Quem | Login | Senha / segredo |
| --- | --- | --- |
| 🧑‍💼 Gestor | `gestor@paradacerta.dev` | `Gestor@123` |
| 📟 Totem | `TOTEM-0001` | `segredo-dev-totem-0001` |

### Pelo `requisicoes.http` (recomendado)

Abra [`requisicoes.http`](requisicoes.http) no VSCode, com a extensão REST Client, e clique em **Send Request**
acima de cada bloco, na ordem. O token do login é reaproveitado automaticamente nos blocos seguintes.

São 10 blocos prontos: login do totem, sinal de cartão, sinal de botão, heartbeat, login do gestor, listar totens,
estado de um totem, cadastrar totem, desativar totem e health checks.

### Pelo Swagger

1. Abra <http://localhost:5117/swagger> (a raiz <http://localhost:5117> também redireciona para lá).
2. Execute `POST /api/auth/dispositivo` com:
   ```json
   {"identificador":"TOTEM-0001","segredo":"segredo-dev-totem-0001"}
   ```
3. Copie o `accessToken`, clique em **Authorize** e cole o token (sem a palavra *Bearer*).
4. Execute `POST /api/sinais` com `Idempotency-Key` igual a `TOTEM-0001-000001` e o corpo:
   ```json
   {"totemId":"TOTEM-0001","tipo":"CARTAO","uidCartao":"04A1B2C3D4","dataHora":"2026-11-18T14:32:05-03:00"}
   ```

### Roteiro de demonstração

| # | O que fazer | Resultado esperado |
| :---: | --- | --- |
| 1 | Login do totem | **200** com `accessToken` |
| 2 | Enviar um sinal com chave nova | **201** + cabeçalho `Location` |
| 3 | Reenviar exatamente a mesma requisição | **200** + `Idempotency-Replayed: true`, sem duplicar |
| 4 | Mesma chave, trocando `CARTAO` por `BOTAO` | **409** |
| 5 | Chamar `/api/sinais` sem token | **401** |
| 6 | Enviar vários sinais seguidos, com chaves diferentes | **429** + `Retry-After` ao passar de 10 no minuto |
| 7 | Enviar um heartbeat | **204** |
| 8 | Login do gestor e `GET /api/totens/1` | `"online": true` |
| 9 | `GET /health` | os quatro itens `Healthy` |
| 10 | Painel do RabbitMQ, fila `sinal-totem` | uma mensagem por sinal novo |

> [!NOTE]
> Reenvios (200) e conflitos (409) também contam para o limite de 10 por minuto, porque cada um é uma requisição.
> Se receber 429 durante os testes, espere o tempo indicado em `Retry-After`.

<details>
<summary><b>Conferir os dados gravados no Oracle, no RabbitMQ e no MongoDB</b></summary>

Oracle: um registro por sinal novo (reenvios não criam linha):

```bash
echo "SELECT id, chave_idempotencia, tipo, recebido_em FROM sinal_totem ORDER BY id;" | docker compose exec -T oracle sqlplus -s parada/parada_dev@localhost/FREEPDB1
```

RabbitMQ: quantidade de mensagens na fila `sinal-totem`:

```bash
docker compose exec rabbitmq rabbitmqctl list_queues name messages
```

MongoDB: eventos por tipo (`SINAL_RECEBIDO`, `SINAL_REPETIDO`, `HEARTBEAT`):

```bash
docker compose exec mongodb mongosh -u parada -p parada_dev --quiet --eval 'db.getSiblingDB("parada_certa").eventos_totem.aggregate([{$group:{_id:"$tipo",total:{$sum:1}}}]).toArray()'
```

</details>

<details>
<summary><b>Testar a resiliência, derrubando cada serviço</b></summary>

| Derrube com | Depois, faça | Resultado esperado |
| --- | --- | --- |
| `docker compose stop rabbitmq` | Envie um sinal | **503**, e nada é gravado no Oracle. Religue com `docker compose start rabbitmq` e reenvie com a **mesma chave**: **201** |
| `docker compose stop mongodb` | Envie um sinal | **201**: o MongoDB não é caminho crítico. O `/health` responde **503** com `mongodb` em `Unhealthy` |
| `docker compose stop oracle` | Faça login | **503** + `Retry-After: 15`, nunca 500 |

Para religar tudo:

```bash
docker compose start
```

</details>

## ✅ Testes automatizados

Requer o .NET SDK 10 (não precisa de Docker):

```bash
dotnet test
```

- **66 testes** de unidade e de integração.
- O Oracle é trocado por **SQLite em memória**, que respeita o índice UNIQUE da idempotência. O RabbitMQ e o
  MongoDB são trocados por implementações falsas.
- Autenticação, autorização, rate limiting, validação e serialização são **os reais** da aplicação.

| Pasta | O que cobre |
| --- | --- |
| [`Unidade/`](tests/ParadaCerta.Dispositivos.Tests/Unidade) | Validação do sinal; hash do cartão (com valor de referência calculado fora do .NET); JWT; regras do `SinalService`; classificação de falha de dependência; precisão das datas |
| [`Integracao/`](tests/ParadaCerta.Dispositivos.Tests/Integracao) | API completa: 201, 200 repetido, 400, 401, 403, 409, 429 e 503; fluxo do gestor; health checks; documento OpenAPI; datas em UTC; proxy reverso |

---

## 📡 Endpoints

| Método | Rota | Perfil | Descrição |
| :---: | --- | :---: | --- |
| `POST` | `/api/auth/dispositivo` | — | Identificador + segredo do totem → JWT `DISPOSITIVO` (24 h) |
| `POST` | `/api/auth/gestor` | — | E-mail + senha (tabela `USUARIO`, BCrypt) → JWT `GESTOR` (2 h) |
| `POST` | `/api/sinais` | DISPOSITIVO | Recebe um sinal de cartão ou botão |
| `POST` | `/api/heartbeats` | DISPOSITIVO | Sinal de vida do totem (gravado só no MongoDB) |
| `GET` | `/api/sinais/{id}` | GESTOR | Consulta um sinal |
| `GET` | `/api/totens?status=` | GESTOR | Lista os totens (filtro opcional `ATIVO` ou `INATIVO`) |
| `GET` | `/api/totens/{id}` | GESTOR | Estado do totem: último sinal, último heartbeat e se está online |
| `POST` | `/api/totens` | GESTOR | Cadastra um totem e devolve o segredo **uma única vez** |
| `PATCH` | `/api/totens/{id}/status` | GESTOR | Ativa ou desativa (`ATIVO` / `INATIVO`) |
| `POST` | `/api/totens/{id}/segredo` | GESTOR | Gera um novo segredo |
| `GET` | `/api/totens/{id}/sinais?limite=` | GESTOR | Últimos sinais do totem (1 a 100, padrão 20) |
| `GET` | `/health` · `/health/live` · `/health/ready` | — | Saúde de tudo · só da API · só das dependências |

### `POST /api/sinais` em detalhe

```http
POST /api/sinais
Authorization: Bearer <token do dispositivo>
Idempotency-Key: TOTEM-0001-000123
Content-Type: application/json

{"totemId":"TOTEM-0001","tipo":"CARTAO","uidCartao":"04A1B2C3D4","dataHora":"2026-11-18T14:32:05-03:00"}
```

| Campo | Regra |
| --- | --- |
| `Idempotency-Key` *(cabeçalho)* | Obrigatório; até 80 caracteres: letras, números, `-`, `_`, `.` ou `:` |
| `totemId` | Obrigatório; precisa ser o mesmo totem do token |
| `tipo` | `CARTAO` ou `BOTAO` |
| `uidCartao` | Obrigatório se `CARTAO` (4 a 10 bytes em hexadecimal); proibido se `BOTAO` |
| `dataHora` | Obrigatória, ISO 8601 com fuso |

| Situação | Resposta |
| --- | --- |
| Chave nova | 🟢 **201** + `Location: /api/sinais/{id}` |
| Mesma chave, mesmo conteúdo (reenvio do firmware) | 🟢 **200** com o mesmo sinal + `Idempotency-Replayed: true` |
| Sem `Idempotency-Key`, tipo inválido ou UID inválido | 🟡 **400** com os erros por campo |
| Sem token ou token inválido | 🟡 **401** |
| Token de outro totem ou de gestor, totem inativo ou não cadastrado | 🟡 **403** |
| Mesma chave, conteúdo diferente | 🟡 **409** |
| Mais de 10 sinais no minuto pelo mesmo totem | 🟡 **429** + `Retry-After` |
| RabbitMQ fora do ar | 🔴 **503**: nada fica gravado; o totem reenvia com a mesma chave |
| Oracle ou outra dependência fora do ar (em qualquer rota) | 🔴 **503** + `Retry-After: 15` |

### Regras gerais

- Todos os erros seguem o padrão **Problem Details** (RFC 9457), com `Content-Type: application/problem+json`.
- As datas das respostas (`recebidoEm`, `criadoEm`...) saem sempre em **UTC**, com `Z` no final.
- Login com campo vazio responde **400**; credencial errada, **401**.

### Limites de requisição

| Política | Limite | Contado por |
| --- | :---: | --- |
| Sinais | 10 por minuto | totem |
| Heartbeat | 6 por minuto | totem |
| Login | 5 por minuto | IP do cliente |

Os valores ficam em [`appsettings.json`](src/ParadaCerta.Dispositivos.Api/appsettings.json) (seção
`LimiteRequisicoes`) e podem ser trocados por variável de ambiente.

---

## 🤝 Integração com o restante do grupo

### Hash do cartão (a API Java precisa calcular igual)

```text
uid_hash = hex_minúsculo( HMAC-SHA256( chave = Cartao:ChaveHmac, mensagem = UTF-8(UID normalizado) ) )
```

- **UID normalizado:** sem espaços, `:` e `-`, em MAIÚSCULAS (`04:a1:b2:c3:d4` → `04A1B2C3D4`).
- **Valor de referência para testar na Java:** chave `chave-hmac-de-teste-com-mais-de-32-caracteres` e UID
  `04A1B2C3D4` → `f2eb07590200c3b0cc629c26b51b3f681d1203a0edd18d675800e0b9c065e2c9`.

> [!IMPORTANT]
> As duas APIs precisam usar **a mesma chave HMAC** em cada ambiente. Caso contrário, o hash gravado pelo totem
> nunca bate com o do cartão cadastrado pelo cuidador.

### Mensagem na fila `sinal-totem`

Fila durável e sem argumentos extras. A mensagem é persistente, com `content-type: application/json` e
`message-id` igual à `Idempotency-Key`.

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
  "recebidoEm": "2026-11-18T17:32:05.123456Z"
}
```

A Java usa `chaveIdempotencia` como `SOLICITACAO_EMBARQUE.CHAVE_IDEMPOTENCIA` e, depois de criar a solicitação,
preenche `SINAL_TOTEM.SOLICITACAO_ID`. Todas as datas gravadas pela API .NET estão em **UTC**.

### Banco de dados

As tabelas vêm do script do grupo, [`db/scripts/01-ddl.sql`](db/scripts/01-ddl.sql). Não há migrations do
EF Core: mudanças de tabela são feitas no DDL e combinadas com o grupo.

| Tabela | Acesso desta API |
| --- | --- |
| `TOTEM` | leitura e escrita |
| `SINAL_TOTEM` | leitura e escrita |
| `PARADA` | somente leitura |
| `USUARIO` | somente leitura (login do gestor) |

---

## 🔧 Configuração

Nada sensível fica no repositório: `.env` e `appsettings.Development.json` são ignorados pelo Git. Em produção,
configure por variáveis de ambiente (o `__` separa as seções):

| Variável | Obrigatória | Exemplo / regra |
| --- | :---: | --- |
| `ConnectionStrings__Oracle` | ✅ | `User Id=...;Password=...;Data Source=host:1521/servico` |
| `ConnectionStrings__MongoDb` | ✅ | `mongodb+srv://...` (Atlas) |
| `RabbitMq__Uri` | ✅ \* | `amqps://usuario:senha@host/vhost` (\* ou `RabbitMq__HostName`, `__Usuario` e `__Senha`) |
| `Jwt__Chave` | ✅ | mínimo 32 caracteres; a API não sobe sem ela |
| `Cartao__ChaveHmac` | ✅ | mínimo 32 caracteres, **igual à da API Java** |
| `ProxyReverso__Habilitado` | — | `true` só atrás do proxy da nuvem (veja abaixo); padrão `false` |
| `LimiteRequisicoes__Sinais__Limite` | — | padrão `10` (há o mesmo para `Heartbeat` e `Login`) |

> [!WARNING]
> **`ProxyReverso__Habilitado`:** na nuvem, as conexões chegam por um proxy, então a API vê o IP do proxy e todos
> os clientes dividiriam o mesmo limite de login. Com `true`, o IP real passa a vir do `X-Forwarded-For`.
> **Não ative** quando a API recebe conexões direto, senão o cliente poderia forjar o próprio IP.

<details>
<summary><b>Variáveis do <code>docker-compose.yml</code> (todas opcionais)</b></summary>

Valem só para o ambiente local. Para trocar, copie [`.env.example`](.env.example) para `.env`.

| Variável | Padrão |
| --- | --- |
| `API_PORTA` | `5117` |
| `ORACLE_PORTA` | `1521` |
| `RABBITMQ_PORTA` / `RABBITMQ_PAINEL_PORTA` | `5672` / `15672` |
| `MONGO_PORTA` | `27018` |
| `ORACLE_APP_USER` / `ORACLE_APP_PASSWORD` | `parada` / `parada_dev` |
| `RABBITMQ_USER` / `RABBITMQ_PASSWORD` | `parada` / `parada_dev` |
| `MONGO_USER` / `MONGO_PASSWORD` | `parada` / `parada_dev` |
| `JWT_CHAVE` / `CARTAO_CHAVE_HMAC` | valores de desenvolvimento |

</details>

## 🌐 Deploy

A imagem Docker é multi-stage e roda **sem usuário root**, na porta 8080 dentro do container:

```bash
docker build -t parada-certa-dispositivos .
```

Para publicar (Azure App Service for Containers, Render ou similar):

1. Tenha Oracle, RabbitMQ (ex.: CloudAMQP) e MongoDB (ex.: Atlas) acessíveis pela internet.
2. Rode [`01-ddl.sql`](db/scripts/01-ddl.sql) no Oracle. **Não** rode o `02-seed-dev.sql` em produção; crie o
   gestor pela API Java ou por um script próprio.
3. Publique a imagem e configure as [variáveis de ambiente](#-configuração) com **chaves novas**, nunca as de
   desenvolvimento.
4. Ative `ProxyReverso__Habilitado=true`.
5. Aponte o health check da plataforma para `/health/live` e confira `/health` depois do deploy.
6. Cadastre os totens reais por `POST /api/totens` e grave cada segredo no firmware.

> [!IMPORTANT]
> A API precisa **ficar no ar durante a correção** da Global Solution.

## 📁 Estrutura do projeto

```text
.
├── src/ParadaCerta.Dispositivos.Api/
│   ├── Program.cs           pipeline HTTP (ordem dos middlewares)
│   ├── Configuracao/        registro de serviços, rate limiting, OpenAPI, Problem Details, falha de dependência
│   ├── Contratos/           DTOs de entrada e saída
│   ├── Dados/               DbContext (mapeamento para as tabelas Oracle) e relógio do banco
│   ├── Dominio/             entidades Totem, SinalTotem, Parada e Usuario
│   ├── Endpoints/           rotas da Minimal API, agrupadas por assunto
│   ├── Eventos/             MongoDB (eventos_totem)
│   ├── Mensageria/          RabbitMQ (fila sinal-totem)
│   ├── Saude/               health checks do RabbitMQ e do MongoDB
│   ├── Seguranca/           JWT, hash do cartão e do segredo do totem
│   └── Servicos/            regras de negócio e validações
├── tests/ParadaCerta.Dispositivos.Tests/
│   ├── Infra/               WebApplicationFactory, SQLite em memória, fakes e dados de teste
│   ├── Unidade/
│   └── Integracao/
├── db/
│   ├── init/                script que o container do Oracle roda na primeira subida
│   └── scripts/             DDL do grupo e seed de desenvolvimento
├── .vscode/                 depuração (F5), tarefas e extensões recomendadas
├── docker-compose.yml       Oracle, RabbitMQ, MongoDB e a API
├── Dockerfile               imagem da API
└── requisicoes.http         requisições prontas para a extensão REST Client
```

## 🧭 Decisões de arquitetura

| Decisão | Por quê |
| --- | --- |
| **Um projeto, pastas por responsabilidade** | Para o tamanho desta API, separar em vários projetos (Clean Architecture completa) traria complexidade sem ganho. As dependências externas ficam atrás de interfaces (`IPublicadorSinais`, `IRegistroEventos`), o que já permite testar sem infraestrutura. |
| **Minimal API com `TypedResults`** | Os tipos de retorno documentam as respostas no OpenAPI e são verificados pelo compilador. |
| **EF Core sem repositórios extras** | O `DbContext` já é unidade de trabalho e repositório; outra camada só repetiria código. |
| **Idempotência no serviço + UNIQUE no banco** | A consulta prévia resolve o caso comum; o UNIQUE é a última barreira quando dois envios chegam ao mesmo tempo. |
| **Publicação com confirmação do broker** | A API só responde 201 depois de o RabbitMQ confirmar que guardou a mensagem. Se falhar, desfaz a gravação e responde 503. |
| **MongoDB só para telemetria** | Se o MongoDB cair, o sinal continua valendo: ele não é caminho crítico. |
| **Dependência fora do ar vira 503** | O cliente sabe que pode tentar de novo (`Retry-After`), em vez de receber um 500 genérico. |
| **UID do cartão em HMAC, segredo do totem em SHA-256** | Nada que identifique o passageiro ou dê acesso ao totem fica gravado em texto puro. |
| **Problem Details em todos os erros** | Formato padrão (RFC 9457), fácil de tratar no firmware, no app e na API Java. |

## 🩺 Problemas comuns

<details>
<summary><b>Ver a lista de sintomas e soluções</b></summary>

| Sintoma | Causa e solução |
| --- | --- |
| `Cannot connect to the Docker daemon` | O Docker Desktop não está aberto. Abra e espere ele terminar de iniciar. |
| `port is already allocated` ou `address already in use` | Outra aplicação usa a porta. Copie `.env.example` para `.env`, troque a porta (`API_PORTA`, `ORACLE_PORTA`, `RABBITMQ_PORTA`, `RABBITMQ_PAINEL_PORTA` ou `MONGO_PORTA`) e rode `docker compose up -d` de novo. |
| `/health` mostra `oracle` como `Unhealthy` | O Oracle ainda está iniciando. Espere o `docker compose ps` mostrar `healthy`. |
| Login ou sinal respondem **503** | Alguma dependência está fora do ar. Veja `/health` e `docker compose ps`. |
| **429** durante os testes manuais | O limite é por minuto: espere o tempo do cabeçalho `Retry-After`. |
| O computador fica lento | O Oracle usa cerca de 2 GB de RAM. No Docker Desktop, reserve pelo menos 4 GB em *Settings → Resources*. |
| No Windows, o Oracle sobe mas as tabelas não existem | O script de inicialização ficou com fim de linha do Windows. O `.gitattributes` evita isso em clones novos; se acontecer, clone de novo e rode `docker compose down -v` antes de subir. |
| Pelo VSCode, a API não sobe e reclama de `Jwt:Chave` ou `Cartao:ChaveHmac` | Falta o `appsettings.Development.json` (passo 2 do modo desenvolvimento) ou a chave tem menos de 32 caracteres. |
| MongoDB não conecta pelo VSCode | Confira se a connection string usa a porta **27018**, a do compose; a 27017 costuma ser de um MongoDB instalado na máquina. |
| Erro de SQL ao criar as tabelas | No SQL\*Plus, um comentário `--` na mesma linha, logo depois de um `;`, quebra o script. Deixe o comentário em uma linha própria e recrie com `docker compose down -v` seguido de `docker compose up -d`. |

</details>

---

## 📋 O que ainda precisa ser feito

### Entrega da Global Solution

- [ ] Criar o arquivo `.txt` com **RM, nome completo e turma** de cada integrante (sem ele, o desconto é de 100 pontos).
- [ ] Montar o `.zip` com o link deste repositório e o `.txt`.
- [ ] Publicar a API na nuvem ([Deploy](#-deploy)) e mantê-la no ar durante a correção.
- [ ] Ensaiar a demonstração seguindo o [roteiro](#roteiro-de-demonstração).
- [ ] Preparar a explicação dos componentes .NET: pipeline de middlewares, injeção de dependência, EF Core, rate limiter, health checks e JWT.

### Combinar com o grupo

- [ ] **Chave HMAC do cartão:** a mesma na API .NET e na API Java em cada ambiente; testar com o [valor de referência](#hash-do-cartão-a-api-java-precisa-calcular-igual).
- [ ] **Contrato da fila `sinal-totem`:** validar com quem faz o consumidor Java.
- [ ] **Firmware do totem:** gerar a `Idempotency-Key` uma vez por acionamento e reutilizá-la nos reenvios; tratar 401 (refazer o login), 429 e 503 (esperar o `Retry-After`).
- [ ] **App Mobile:** definir se alguma tela usa esta API (por exemplo, a gestão de totens pelo gestor).
- [ ] Corrigir no DDL original do grupo o comentário depois de `;` em `idx_tentativa_veic_res`.

### Melhorias técnicas planejadas

| Prioridade | Melhoria | Motivo |
| :---: | --- | --- |
| 🔴 Alta | **Transactional Outbox:** coluna `PUBLICADO_EM` em `SINAL_TOTEM` e um serviço em segundo plano que publica os pendentes | Se a API cair entre gravar no Oracle e publicar no RabbitMQ, o sinal fica gravado sem ir para a fila, e o reenvio responde 200 sem publicar. **Exige mudança no DDL do grupo.** |
| 🔴 Alta | Idempotência por totem: `UNIQUE(totem_id, chave_idempotencia)` | Hoje a chave vale para todos os totens; chaves iguais de totens diferentes geram 409. **Exige mudança no DDL.** |
| 🟡 Média | Conferir se o totem está ativo também no heartbeat e invalidar tokens ao trocar o segredo | Um totem desativado continua com token válido por até 24 h. |
| 🟡 Média | Tempo de resposta igual no login do gestor, exista ou não o e-mail | Evita descobrir e-mails cadastrados pelo tempo de resposta. |
| 🟡 Média | Índice e TTL na coleção `eventos_totem` | A consulta do último heartbeat fica mais lenta conforme a coleção cresce. |
| 🟡 Média | CI no GitHub Actions com `dotnet build` e `dotnet test` | Mostra a cada push que os testes passam. |
| 🟢 Baixa | Reaproveitar o canal do RabbitMQ e declarar a fila uma vez só | Hoje cada publicação abre um canal e redeclara a fila. |
| 🟢 Baixa | Testes de integração com Oracle real (Testcontainers) | O SQLite dos testes não reproduz todos os comportamentos do Oracle. |
| 🟢 Baixa | Paginação em `GET /api/totens` e campo `versao` na mensagem da fila | Crescimento e evolução do contrato. |

---

<div align="center">

Feito por **[Maicon Douglas](https://github.com/MaiconDouglas-dev)** · API Dispositivos (.NET) · Global Solution 2026/2 · FIAP

</div>
