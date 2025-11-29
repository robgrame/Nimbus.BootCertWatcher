# WindowsVersionsCore Project Instructions

This project is an ASP.NET Core web application for tracking Windows 10 and Windows 11 releases and updates, designed for the IT Administrator community.

## Project Overview
- **Technology Stack**: ASP.NET Core 8.0 with Razor Pages and Web API
- **Purpose**: Provide comprehensive Windows release information for IT administrators
- **Data Sources**: Web scraping from Microsoft's official Windows update documentation
- **Architecture**: Similar to OfficeVersionsCore with modern, clean design

## Key Features
- Windows 10 and Windows 11 release tracking
- Automated web scraping of Microsoft documentation
- RESTful API for programmatic access
- Modern responsive UI for IT administrators
- Background services for data updates
- Azure Storage integration
- Application Insights telemetry

## Data Sources
1. Windows 10 Update History: https://support.microsoft.com/en-us/topic/windows-10-update-history-8127c2c6-6edf-4fdf-8b9f-0f7be1ef3562
2. Windows 11 Update History: https://support.microsoft.com/en-us/topic/windows-11-version-24h2-update-history-0929c747-1815-4543-8461-0160d16f15e5
3. Windows 10 Release Information: https://learn.microsoft.com/en-us/windows/release-health/release-information
4. Windows 11 Release Information: https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information

## Development Guidelines
- Follow .NET 8 best practices
- Implement proper error handling and logging
- Use dependency injection for services
- Include comprehensive API documentation with Swagger
- Ensure mobile-responsive design
- Implement proper caching strategies for scraped data