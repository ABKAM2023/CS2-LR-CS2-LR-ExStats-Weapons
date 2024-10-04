**[C#] [LR] ExStats Weapons** module collects additional weapon statistics and saves all data in a separate table in the database. You can also configure how much experience will be awarded for kills with specific weapons.

# Installation
1. Install [C# Levels Ranks Core](https://github.com/ABKAM2023/CS2-LevelsRanks-Core/tree/v1.0)
2. Download [C#] [LR] ExStats Weapons
3. Unpack the archive and upload it to the game server
4. Configure exstats_weapons.json
5. Restart the server

# Configuration file (exstats_weapons.json)
```json
{
  "ExperienceEnabled": true, // Enable/disable experience awarding for kills with specific weapons
  "Weapons": {
      "weapon_knife": {
      "Name": "for killing with Knife", // Message that will be displayed. Example: [LR] Your experience: 28060 [+10 for killing with Knife]
      "Color": "{Green}", // Text color that will appear in the message [+10 for killing with Knife]
      "Exp": 10 // Amount of experience awarded for killing with a knife
    },
    "weapon_taser": {
      "Name": "for killing with Zeus x27",
      "Color": "{Yellow}",
      "Exp": 8
    },
    "weapon_inferno": {
      "Name": "for killing with Molotov",
      "Color": "{Red}",
      "Exp": 7
    },
    "weapon_hegrenade": {
      "Name": "for killing with Hegrenade",
      "Color": "{Orange}",
      "Exp": 5
    },
    .......
}
```

# RU
**[C#] [LR] ExStats Weapons** модуль собирает дополнительную статистику по оружию и сохраняет все данные в отдельной таблице в базе данных. Также можно настроить, сколько опыта будет начисляться за убийства с определённого оружия.

# Установка
1. Установите [C# Levels Ranks Core](https://github.com/ABKAM2023/CS2-LevelsRanks-Core/tree/v1.0)
2. Скачайте [C#] [LR] ExStats Weapons
3. Распакуйте архив и загрузите его на игровой сервер
4. Настройте exstats_weapons.json
5. Перезапустите сервер

# Конфигурационный файл (exstats_weapons.json)
```json
{
  "ExperienceEnabled": true, // Включить/выключить начисление опыта за убийство с определённого оружия
  "Weapons": {
      "weapon_knife": {
      "Name": "за убийство с Knife", // Сообщение, которое будет отображаться. Пример: [LR] Ваш опыт: 28060 [+10 за убийство с Knife]
      "Color": "{Green}", // Цвет текста, который будет отображаться в сообщении [+10 за убийство с Knife]
      "Exp": 10 // Количество опыта, которое будет начисляться за убийство ножом
    },
    "weapon_taser": {
      "Name": "за убийство с Zeus x27",
      "Color": "{Yellow}",
      "Exp": 8
    },
    "weapon_inferno": {
      "Name": "за убийство с Molotov",
      "Color": "{Red}",
      "Exp": 7
    },
    "weapon_hegrenade": {
      "Name": "за убийство с Hegrenade",
      "Color": "{Orange}",
      "Exp": 5
    },
    .......
}
```
