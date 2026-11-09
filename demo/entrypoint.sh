#!/bin/sh
# Links persistent folders into /data and starts Umbraco. On first boot Umbraco installs
# itself unattended (admin from DEMO_ADMIN_*) and the Clean starter kit imports its content;
# the demo's DemoSetupService then adds the languages and the editor account on every start.
set -e

APP=/app
DATA=/data
mkdir -p "$DATA/Data" "$DATA/media" "$DATA/Logs" "$APP/umbraco" "$APP/wwwroot"
for pair in "umbraco/Data:Data" "umbraco/Logs:Logs" "wwwroot/media:media"; do
  target="$APP/${pair%%:*}"; source="$DATA/${pair##*:}"
  [ -L "$target" ] || rm -rf "$target"
  ln -sfn "$source" "$target"
done

# Admin account for the unattended install (first boot only; DemoSetupService also creates
# it on later starts if it's missing).
if [ ! -s "$DATA/Data/Umbraco.sqlite.db" ]; then
  if [ -z "$DEMO_ADMIN_EMAIL" ] || [ -z "$DEMO_ADMIN_PASSWORD" ]; then
    echo "DEMO_ADMIN_EMAIL / DEMO_ADMIN_PASSWORD are not set - refusing to install without an admin account." >&2
    exit 1
  fi
  echo "First boot: installing Umbraco with the Clean starter kit..."
fi
export Umbraco__CMS__Unattended__UnattendedUserEmail="${DEMO_ADMIN_EMAIL:-}"
export Umbraco__CMS__Unattended__UnattendedUserPassword="${DEMO_ADMIN_PASSWORD:-}"

# Umbraco (17 and 18) doesn't detect its own URL; the backoffice login needs it.
if [ -z "$Umbraco__CMS__WebRouting__UmbracoApplicationUrl" ]; then
  if [ -n "$APP_URL" ]; then
    export Umbraco__CMS__WebRouting__UmbracoApplicationUrl="$APP_URL"
  elif [ -n "$RAILWAY_PUBLIC_DOMAIN" ]; then
    export Umbraco__CMS__WebRouting__UmbracoApplicationUrl="https://$RAILWAY_PUBLIC_DOMAIN"
  fi
fi
[ -n "$PORT" ] && export ASPNETCORE_URLS="http://+:$PORT"

cd "$APP"
exec dotnet SupertextDemo.dll
