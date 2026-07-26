FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj ก่อนโค้ด เพื่อให้ layer ของ restore ถูก cache ไว้เมื่อแก้แต่โค้ด
COPY meesuanruam-service.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# image ของ .NET 8 ฟัง 8080 และรันด้วย user ชื่อ app (ไม่ใช่ root) มาให้อยู่แล้ว
EXPOSE 8080

ENTRYPOINT ["dotnet", "meesuanruam-service.dll"]
