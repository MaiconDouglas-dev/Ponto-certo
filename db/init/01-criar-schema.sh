#!/bin/bash
# Executado UMA vez pelo container gvenzl/oracle-free, quando o volume do banco é criado.
# Roda os scripts de db/scripts (montados em /opt/parada-certa/scripts) com o usuário da aplicação.
set -e

for script in /opt/parada-certa/scripts/*.sql; do
  echo ">>> Executando ${script}"
  sqlplus -s "${APP_USER}/${APP_USER_PASSWORD}@//localhost/FREEPDB1" <<SQL
WHENEVER SQLERROR EXIT SQL.SQLCODE
SET DEFINE OFF
@${script}
EXIT
SQL
done
