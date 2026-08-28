## Creacion de la tablas normalmente

dotnet ef migrations add 1erMigra --context \_20262.Data.ApplicationDbContext -o "D:\Root\Code\usmp\20262\Data\Migrations"

## cuando no lo tienes instalado

dotnet tool install --global dotnet-ef

dotnet tool update --global dotnet-ef

## ejecuta la creacion de tablas en la base datos

dotnet ef database update
