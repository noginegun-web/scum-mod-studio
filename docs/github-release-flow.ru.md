# Публичные релизы GitHub при закрытых исходниках

GitHub не умеет делать обычный Release приватного репозитория публичным для всех. Если репозиторий публичный, GitHub автоматически показывает архивы исходников (`Source code.zip/.tar.gz`) у каждого релиза. Поэтому рабочая схема такая:

1. `noginegun-web/scum-mod-studio` - приватный репозиторий с исходниками.
2. `noginegun-web/scum-mod-studio-releases` - публичный репозиторий без исходников, только GitHub Releases с файлами Velopack.
3. В `appsettings.json` и релизной сборке `RepoUrl` должен указывать на публичный release-only репозиторий.

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

Токену нужны права на создание релизов и загрузку assets в публичный release-only репозиторий.

## Создать публичный репозиторий только для релизов

```powershell
gh repo create noginegun-web/scum-mod-studio-releases --public --description "Public binary releases for SCUM Mod Studio" --confirm
```

В этот репозиторий не нужно пушить исходники проекта. Достаточно README или даже пустого репозитория: Velopack будет загружать файлы в GitHub Releases.

## Сделать исходники приватными

Перед этим убедиться, что публичный release-only репозиторий создан, а новая версия программы уже смотрит на него:

```powershell
gh repo edit noginegun-web/scum-mod-studio --visibility private --accept-visibility-change-consequences
```

Если старый репозиторий оставить публичным, исходники останутся видны. Если сделать его приватным до переноса обновлений, клиенты потеряют доступ к релизам старого URL.

## Собрать и опубликовать обновление

Из корня проекта:

```powershell
$env:GH_TOKEN = "github_pat_with_repo_access"
pwsh -ExecutionPolicy Bypass -File .\scripts\test_release_gate.ps1 -Configuration Release -SchemaSampleSize 4
pwsh -ExecutionPolicy Bypass -File .\scripts\publish_update_release.ps1 `
  -RepoUrl "https://github.com/noginegun-web/scum-mod-studio-releases" `
  -PublishToGithub
```

Что важно:

- `scripts\publish_update_release.ps1` уже по умолчанию использует `https://github.com/noginegun-web/scum-mod-studio-releases`.
- Для полного аудита перед большим релизом использовать:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\test_release_gate.ps1 `
  -Configuration Release `
  -FullAssetAudit `
  -AuditThrottle 10 `
  -MinFullAuditAssets 8000 `
  -AuditReportPath .codex-audit\release-gate-schema-full.json
```

- Номер версии берётся из `ScumPakWizard.csproj`: `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`.
- Тег релиза будет `v<Version>`, например `v0.1.16`.
- Установленная версия проверяет обновления через Velopack и публичный `RepoUrl`.

## Если другой агент не может запостить проект

Проверить по порядку:

```powershell
gh --version
gh auth status
git remote -v
gh repo view noginegun-web/scum-mod-studio-releases
```

Типовые причины:

- агент авторизован в браузере, но не в `gh`;
- нет `GH_TOKEN` или у токена нет прав на нужный репозиторий;
- агент пытается публиковать в приватный исходный репозиторий вместо публичного `scum-mod-studio-releases`;
- в релизной сборке указан старый `RepoUrl`;
- версия в `ScumPakWizard.csproj` не увеличена, и GitHub/Velopack видит уже существующий тег.
