# Релизы GitHub и автообновление

Обновления SCUM Mod Studio публикуются в основной репозиторий:

```text
https://github.com/noginegun-web/scum-mod-studio
```

Это важно: установленная программа проверяет Velopack-релизы по `RepoUrl` из `appsettings.json`, поэтому release assets (`releases.win.json`, `RELEASES`, `.nupkg`, setup и portable zip) должны лежать именно в `noginegun-web/scum-mod-studio`.

## Одноразовая настройка GitHub CLI

```powershell
gh auth login
gh auth status
```

Если агент работает без интерактивного входа, вместо этого нужен токен:

```powershell
$env:GH_TOKEN = "github_pat_with_repo_access"
gh auth status
```

Токену нужны права на push тегов, создание Releases и загрузку release assets в `noginegun-web/scum-mod-studio`.

## Собрать и опубликовать обновление

Из корня проекта:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test_release_gate.ps1 -Configuration Release -SchemaSampleSize 4
powershell -ExecutionPolicy Bypass -File .\scripts\publish_update_release.ps1 -PublishToGithub
```

Что важно:

- `scripts\publish_update_release.ps1` по умолчанию использует `https://github.com/noginegun-web/scum-mod-studio`.
- `appsettings.json` в релизной сборке тоже должен указывать на `https://github.com/noginegun-web/scum-mod-studio`.
- Номер версии берётся из `ScumPakWizard.csproj`: `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`.
- Тег релиза будет `v<Version>`, например `v0.1.18`.
- Если тег или release уже существуют, нужно поднять версию или осознанно удалить ошибочный draft/release перед повторной публикацией.

## Полный аудит перед большим релизом

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test_release_gate.ps1 `
  -Configuration Release `
  -FullAssetAudit `
  -AuditThrottle 10 `
  -MinFullAuditAssets 8000 `
  -AuditReportPath .codex-audit\release-gate-schema-full.json
```

## Если публикация не проходит

Проверить по порядку:

```powershell
gh --version
gh auth status
git remote -v
gh repo view noginegun-web/scum-mod-studio
```

Типовые причины:

- агент авторизован в браузере, но не в `gh`;
- нет `GH_TOKEN` или у токена нет прав на нужный репозиторий;
- релиз пытаются публиковать в `scum-mod-studio-releases` вместо `scum-mod-studio`;
- в релизной сборке указан старый `RepoUrl`;
- версия в `ScumPakWizard.csproj` не увеличена, и GitHub/Velopack видит уже существующий тег.
