# ============================================================
# مرحله ۱: محیط Build و Restore پکیج‌ها با دات‌نت ۱۰
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# استفاده از حافظه کش داکر برای پکیج‌ها
COPY ["iptv.Api/iptv.Api.csproj", "iptv.Api/"]
COPY ["iptv.Services/iptv.Services.csproj", "iptv.Services/"]
COPY ["iptv.Domain/iptv.Domain.csproj", "iptv.Domain/"]
COPY ["Utilities/Utilities.csproj", "Utilities/"]

RUN dotnet restore "iptv.Api/iptv.Api.csproj"

# کپی کل سورس‌کد
COPY . .

# بیلد نهایی به صورت بهینه شده
WORKDIR "/src/iptv.Api"
RUN dotnet publish "iptv.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============================================================
# مرحله ۲: محیط سبک و امن Runtime با دات‌نت ۱۰
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# ------------------------------------------------------------
# 🚀 بهینه‌سازی‌های حیاتی دات‌نت برای سرورهای کم‌رم (زیر ۵۱۲ مگابایت)
# ------------------------------------------------------------
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
# فعال‌سازی قابلیت جهانی‌سازی (فارسی‌سازی تاریخ‌ها)
ENV DOTNET_RUN_SYSTEM_GLOBALIZATION_INVARIANT=false
# غیرفعال کردن Server GC برای کاهش شدید مصرف رم (حیاتی برای سرور ابری)
ENV DOTNET_gcServer=0
# بهینه‌سازی آزادسازی حافظه به محض عدم نیاز
ENV DOTNET_GCLatencyMode=0

# کپی فایل‌ها از مرحله بیلد
COPY --from=build /app/publish .

# رعایت اصول امنیتی: سوییچ به کاربر غیر روت جهت امنیت حداکثری
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "iptv.Api.dll"]