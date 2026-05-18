FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY . .

USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
