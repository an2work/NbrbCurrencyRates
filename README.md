# NbrbCurrencyRates

WPF-приложение для загрузки и визуализации курсов валют Национального банка Республики Беларусь.

## Требования

- Windows
- Visual Studio с поддержкой .NET Framework 4.7.2
- Восстановление NuGet-пакетов из `NbrbCurrencyRates/packages.config`
- Действующая лицензия Telerik UI for WPF для использования Telerik-сборок из `lib/Telerik`

## Сборка

1. Откройте `NbrbCurrencyRates.sln` в Visual Studio.
2. Восстановите NuGet-пакеты.
3. Выберите конфигурацию `Debug` или `Release`.
4. Соберите проект `NbrbCurrencyRates`.

Локальные Telerik DLL находятся в `lib/Telerik` и подключаются из проекта относительными путями.
