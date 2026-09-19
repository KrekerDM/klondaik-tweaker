Первая версия. Твикер Windows 10/11, который помнит, что он сделал: перед каждым изменением прежнее значение ключа, службы или задачи уходит в журнал, и любой твик откатывается по отдельности.

## Возможности

- **148 твиков** в трёх уровнях риска, каждый с описанием: что делает, зачем и чем вы за это платите.
- **Подбор по вопросам** — до 16 вопросов, лишние отсеиваются по железу. Ничего не применяется, пока вы не нажмёте кнопку.
- **Категория «Слабый ПК»** — визуальные эффекты, эскизы в проводнике, сжатие памяти, файл подкачки: то, что реально ускоряет старое железо.
- **Откат каждого твика** — журнал хранит прежнее состояние, повторное применение не затирает исходную запись.
- **Права TrustedInstaller** — ключи и службы, которые не поддаются администратору, берутся токеном системной службы.
- **Инструменты** — разовая починка того, что сломали другие твикеры: 232 службы к штатным значениям, Защитник, обновления, Store, поиск, сетевой стек.
- **Очистка, автозагрузка, службы, встроенные приложения, сеть, бенчмарк, каталог софта** — каждое действие тоже пишется в журнал.
- **Обновление по кнопке** — программа сама находит новый релиз, показывает изменения, скачивает и перезапускается.
- **Русский и английский** интерфейс.

## Установка

Скачайте `KlondaikTweaker.exe` и запустите от имени администратора. Устанавливать нечего, файл один, настройки и журнал лежат в `%ProgramData%\KlondaikTweaker`.

При первом запуске программа покажет предупреждение о рисках — его нужно принять.

Windows SmartScreen может предупредить о неизвестном издателе: сборка не подписана сертификатом. «Подробнее» → «Выполнить в любом случае».

**Требования:** Windows 10 1903 или новее либо Windows 11, 64 бита, Microsoft Edge WebView2 Runtime (в Windows 11 уже есть).

## Предупреждение

Программа меняет реестр, службы, задания планировщика, установленные пакеты и сетевые настройки. Такие изменения могут сделать систему нестабильной, отключить защиту и помешать установке обновлений. Вы используете её на свой страх и риск, правообладатель не несёт ответственности за последствия. Журнал и точка восстановления снижают риск, но ничего не гарантируют.

## Что проверено

На Windows 10 21H1 в виртуальной машине: 149 из 149 твиков применились и откатились с побайтовым совпадением состояния, 26 из 26 проверок бэкенда, 23 из 23 модулей. Удаление встроенных приложений и установка софта через winget не проверены: в тестовой машине не осталось удаляемых пакетов, а winget в том образе отсутствует.

---
## English

First release. A Windows 10/11 tweaker that remembers what it did: before every change the previous value of the key, service or task goes into a journal, and any tweak rolls back on its own.

## Features

- **148 tweaks** across three risk levels, each describing what it does, why, and what it costs.
- **Question-based setup** — up to 16 questions, the irrelevant ones dropped based on your hardware. Nothing is applied until you press the button.
- **A "Weak PC" category** — visual effects, Explorer thumbnails, memory compression, page file: things that genuinely speed up old hardware.
- **Per-tweak rollback** — the journal keeps the previous state, and applying a tweak twice does not overwrite the original record.
- **TrustedInstaller rights** — keys and services that do not yield to an administrator are written under the system service's token.
- **Repair tools** — one-click fixes for what other tweakers broke: 232 services back to stock, Defender, updates, Store, search, network stack.
- **Cleanup, startup, services, built-in apps, network, benchmark, software catalogue** — every action is journalled too.
- **Updates on a button** — the program finds a new release, shows the changes, downloads it and restarts.
- **Russian and English** interface.

## Install

Download `KlondaikTweaker.exe` and run it as administrator. There is nothing to install, it is one file, and settings and the journal live in `%ProgramData%\KlondaikTweaker`.

On first run the program shows a warning about the risks, which has to be accepted.

Windows SmartScreen may warn about an unknown publisher: the build is not signed. "More info" → "Run anyway".

**Requirements:** Windows 10 1903 or newer, or Windows 11, 64-bit, Microsoft Edge WebView2 Runtime (already present on Windows 11).

## Warning

The program changes the registry, services, scheduled tasks, installed packages and network settings. Such changes can make the system unstable, disable protection and block updates. You use it entirely at your own risk and the copyright holder is not responsible for the consequences. The journal and the restore point reduce the risk but guarantee nothing.

## What was tested

On Windows 10 21H1 in a virtual machine: 149 of 149 tweaks applied and reverted with the state matching byte for byte, 26 of 26 backend checks, 23 of 23 modules. Built-in app removal and winget installs are not verified: the test machine had no removable packages left and that image has no winget.
