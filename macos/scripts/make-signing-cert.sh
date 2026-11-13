#!/usr/bin/env bash
# Cria, uma vez, o certificado local "LG AC Dev" que o build-app.sh usa para assinar o app.
# Com a assinatura ad-hoc, cada build parece outro app para o Keychain, que pede a senha do Mac
# a cada reinstalação; assinado sempre com o mesmo certificado, o app é reconhecido entre builds.
# O certificado é autoassinado e só vale neste Mac (não é a notarização da Apple).
set -euo pipefail

name="LG AC Dev"
keychain="$HOME/Library/Keychains/login.keychain-db"

if security find-identity -p codesigning "$keychain" | grep -F "\"$name\"" >/dev/null; then
  echo "✓ O certificado \"$name\" já existe"
  exit 0
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# Usa o openssl do sistema (LibreSSL): o .p12 dele é compatível com o `security import`.
/usr/bin/openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -subj "/CN=$name" \
  -addext "basicConstraints=critical,CA:false" \
  -addext "keyUsage=critical,digitalSignature" \
  -addext "extendedKeyUsage=critical,codeSigning" \
  -keyout "$tmp/key.pem" -out "$tmp/cert.pem" 2>/dev/null
/usr/bin/openssl pkcs12 -export -inkey "$tmp/key.pem" -in "$tmp/cert.pem" -name "$name" \
  -passout pass:lgac -out "$tmp/cert.p12"
security import "$tmp/cert.p12" -k "$keychain" -P lgac -T /usr/bin/codesign >/dev/null
echo "✓ Certificado \"$name\" criado no Keychain"

# Libera a chave para o codesign, para ele não perguntar a cada build.
echo "Digite a senha do Mac para o codesign poder usar o certificado sem perguntar:"
if ! security set-key-partition-list -S apple-tool:,apple: -s -l "$name" "$keychain" >/dev/null; then
  echo "Sem a senha, o macOS vai perguntar no primeiro build: digite a senha e clique em Sempre Permitir."
fi
echo "Pronto. Rode ./build-app.sh para reinstalar o app assinado com o certificado."
