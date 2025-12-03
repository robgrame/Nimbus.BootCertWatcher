# Secure Boot Dashboard - Executive Brief

**Enterprise UEFI Certificate Monitoring & Management**  
**Version 1.12.0**  
**Document Version**: 1.0  
**Date**: January 2025

---

## Executive Summary

**Secure Boot Dashboard** is an enterprise-grade solution designed to monitor and manage UEFI Secure Boot certificates across Windows device fleets. As Microsoft rolls out the UEFI CA 2023 certificate update, organizations face the challenge of ensuring device compliance without risking boot failures or hardware issues.

### The Business Case

**Problem**:
- No native Windows tool for fleet-wide UEFI certificate monitoring
- Expired OEM certificates can prevent devices from booting
- Manual certificate inventory is time-consuming and error-prone
- UEFI CA 2023 rollout requires proactive deployment readiness assessment

**Solution**:
Secure Boot Dashboard provides centralized visibility, automated compliance tracking, and remote management capabilities to ensure seamless certificate updates across your entire Windows fleet.

**ROI**:
- **Time Savings**: Reduce audit time from weeks to minutes
- **Risk Mitigation**: Identify at-risk devices before boot failures occur
- **Compliance**: Automated reporting for security audits
- **Efficiency**: Remote command execution eliminates manual device visits

---

## What is Secure Boot Dashboard?

Secure Boot Dashboard is a three-tier enterprise application that:

1. **Monitors** - Continuously tracks UEFI Secure Boot certificate status across all Windows devices
2. **Analyzes** - Evaluates device readiness for certificate updates based on firmware, OS, and certificate criteria
3. **Manages** - Enables remote certificate updates and configuration changes through a centralized web interface

### Key Capabilities

| Capability | Description | Business Value |
|------------|-------------|----------------|
| **Real-time Monitoring** | SignalR-powered live dashboard updates | Instant visibility into fleet status |
| **Certificate Intelligence** | Extracts and analyzes X.509 certificates from 4 UEFI databases (db, dbx, KEK, PK) | Complete certificate inventory |
| **Readiness Assessment** | Multi-criteria evaluation (Firmware + OS + Certificates) | Prioritize deployments effectively |
| **Batch Commands** | Send configuration updates to multiple devices simultaneously | Scale operations efficiently |
| **Data Export** | Professional Excel/CSV reports with color-coding | Audit-ready compliance documentation |
| **Admin Portal** | Database-driven settings with audit trail | Dynamic configuration without downtime |

---

## Core Features

### 1. Certificate Tracking & Expiration Monitoring

**What it does**:
- Enumerates all UEFI Secure Boot certificates from device firmware
- Identifies Microsoft vs. OEM certificates
- Tracks expiration dates with configurable warning thresholds (365 days critical, 1095 days warning)

**Why it matters**:
- Proactively identify devices with expiring certificates
- Prevent boot failures caused by expired OEM certificates
- Maintain compliance with security policies

**Visual Indicators**:
- ? **Green**: All certificates valid
- ?? **Yellow**: Certificates expiring soon (within 3 years)
- ? **Red**: Expired or critically expiring certificates (within 1 year)

---

### 2. Deployment Readiness Assessment

**What it does**:
- Evaluates device readiness for UEFI CA 2023 certificate update
- Validates firmware release date (must be >= January 1, 2024)
- Checks OS build number against minimum secure versions
- Confirms presence of Windows UEFI CA 2023 certificate

**Why it matters**:
- Identify which devices can safely receive certificate updates
- Prioritize firmware updates for devices with outdated BIOS
- Plan OS upgrades for devices on unsupported Windows builds

**Readiness Criteria**:
| Status | Meaning | Action Required |
|--------|---------|-----------------|
| ? **Ready** | Firmware + OS + Certificates compliant | Proceed with certificate update |
| ?? **Partial** | Firmware OR OS ready (not both) | Update firmware or Windows |
| ? **Not Ready** | Neither firmware nor OS compliant | Update both firmware and OS |
| ? **Unknown** | Missing data | Re-run client inventory |

---

### 3. Real-time Dashboard

**What it does**:
- Live updates via SignalR WebSocket technology
- Interactive Chart.js visualizations showing compliance trends
- Toast notifications for critical events
- Auto-reconnection with exponential backoff

**Why it matters**:
- Monitor fleet status without page refreshes
- Receive instant alerts when new devices report
- Drill down from metrics to individual device details

**Dashboard Metrics**:
- Total devices monitored
- Certificate compliance percentage
- Deployment readiness distribution
- Recent report activity
- Command execution statistics

---

### 4. Remote Command Management

**What it does**:
- Send configuration commands to one or multiple devices
- Certificate update commands with force option
- Microsoft Update Opt-In/Out configuration
- Telemetry level management

**Why it matters**:
- Eliminate manual device visits for configuration changes
- Execute batch operations across hundreds of devices
- Track command execution status in real-time
- Maintain complete audit trail of all commands

**Command Types**:
1. **Certificate Update**: Deploy UEFI CA 2023 certificate
2. **Update Configuration**: Opt-in/out of Microsoft Update for controlled rollout
3. **Telemetry Settings**: Configure Windows diagnostic data levels

---

### 5. Data Export & Reporting

**What it does**:
- Export device list to Excel with professional formatting
- Color-coded status indicators for quick visual analysis
- CSV exports for custom data analysis
- Per-device certificate history

**Why it matters**:
- Generate audit-ready compliance reports
- Share data with stakeholders in familiar formats
- Integrate with existing reporting tools

**Export Formats**:
- **Excel (.xlsx)**: Color-coded cells, frozen headers, auto-sized columns
- **CSV (.csv)**: UTF-8 with BOM for Excel compatibility

---

### 6. Admin Configuration Portal

**What it does**:
- Database-driven application settings (12 settings across 3 categories)
- Dynamic configuration without application restart
- Audit trail tracking who changed what and when
- Sensitive value masking for security

**Why it matters**:
- Update client version requirements without redeployment
- Adjust queue processing intervals on-the-fly
- Modify certificate expiration thresholds as policies change
- Track all configuration changes for compliance

**Setting Categories**:
- **QueueProcessor**: Azure Queue settings, message processing intervals
- **ClientUpdate**: Latest client version, download URL, update requirements
- **SecureBootReadiness**: Certificate expiration thresholds, readiness criteria

---

## Architecture Overview

### High-Level Components

```
???????????????????????????????????????????????????????????????
?                    Windows Devices                          ?
?  ????????????????????????????????????????????????????????   ?
?  ?  Secure Boot Watcher Client (.NET Framework 4.8)    ?   ?
?  ?  • Registry snapshot collection                     ?   ?
?  ?  • PowerShell certificate enumeration               ?   ?
?  ?  • Event log capture                                ?   ?
?  ?  • Command processing                               ?   ?
?  ????????????????????????????????????????????????????????   ?
???????????????????????????????????????????????????????????????
                        ?
                        ?
        ?????????????????????????????????????
        ?    Azure Storage Queue            ?
        ?    (Optional Buffering Layer)     ?
        ?????????????????????????????????????
                        ?
                        ?
???????????????????????????????????????????????????????????????
?              Backend API (ASP.NET Core 10)                  ?
?  ????????????????????????????????????????????????????????   ?
?  ?  • REST API endpoints                                ?   ?
?  ?  • EF Core + SQL Server database                     ?   ?
?  ?  • Queue processor service                           ?   ?
?  ?  • SignalR Hub for real-time                         ?   ?
?  ?  • Command management                                ?   ?
?  ????????????????????????????????????????????????????????   ?
???????????????????????????????????????????????????????????????
                        ?
                        ?
???????????????????????????????????????????????????????????????
?            Web Dashboard (Razor Pages)                      ?
?  ????????????????????????????????????????????????????????   ?
?  ?  • Interactive charts and metrics                    ?   ?
?  ?  • Real-time updates via SignalR                     ?   ?
?  ?  • Device and certificate details                    ?   ?
?  ?  • Command interface                                 ?   ?
?  ?  • Settings management                               ?   ?
?  ?  • Excel/CSV export                                  ?   ?
?  ????????????????????????????????????????????????????????   ?
???????????????????????????????????????????????????????????????
```

### Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Client** | .NET Framework 4.8 | Windows device agent |
| **API** | ASP.NET Core 10 | Backend services |
| **Web** | Razor Pages + Bootstrap 5 | User interface |
| **Database** | SQL Server 2019+ or Azure SQL | Data persistence |
| **Queue** | Azure Storage Queue | Scalable message buffering |
| **Real-time** | SignalR WebSocket | Live dashboard updates |
| **Charts** | Chart.js 4.4 | Data visualization |
| **Logging** | Serilog + CMTrace format | Diagnostics and audit |

---

## Deployment Options

### 1. Cloud Deployment (Recommended for Enterprise)

**Architecture**:
- API hosted on Azure App Service (Linux or Windows)
- Database: Azure SQL Database with geo-replication
- Queue: Azure Storage Queue with managed identity authentication
- Web Dashboard: Azure App Service with Entra ID authentication
- Client: Deployed via Intune as Scheduled Task

**Benefits**:
- ? Auto-scaling based on demand
- ? Built-in high availability (99.95% SLA)
- ? Managed identity eliminates credential management
- ? Azure Monitor integration for diagnostics

**Estimated Cost** (1,000 devices):
- App Service (B1): $13/month
- Azure SQL (S1): $30/month
- Storage Queue: <$1/month
- **Total**: ~$44/month

---

### 2. On-Premises Deployment

**Architecture**:
- API hosted on IIS or Windows Server
- Database: SQL Server 2019+ on dedicated server
- Queue: Optional (can use direct HTTP)
- Web Dashboard: IIS with Windows Authentication
- Client: Deployed via Group Policy or SCCM

**Benefits**:
- ? No external dependencies
- ? Full control over infrastructure
- ? Integration with existing AD/SCCM
- ? Compliance with air-gapped network requirements

**Requirements**:
- Windows Server 2019+ with IIS 10
- SQL Server 2019+ (Standard or Enterprise)
- .NET 10 Runtime
- 4 GB RAM minimum, 8 GB recommended

---

### 3. Hybrid Deployment

**Architecture**:
- Queue in Azure (scalability + reliability)
- API and Database on-premises (data sovereignty)
- Client reports to Azure Queue, API processes locally

**Benefits**:
- ? Best of both worlds
- ? Data stays on-premises
- ? Queue handles internet connectivity issues
- ? Flexible failover options

---

## Security & Compliance

### Authentication & Authorization

| Feature | Implementation | Benefit |
|---------|---------------|---------|
| **User Authentication** | Entra ID (Azure AD) or Windows Domain | Single sign-on, MFA support |
| **API Authentication** | Certificate-based or Managed Identity | Secure service-to-service auth |
| **Role-Based Access** | Admin vs. Operator roles | Principle of least privilege |
| **Audit Logging** | Serilog structured logging | Complete activity trail |

### Data Protection

| Layer | Protection | Details |
|-------|-----------|---------|
| **In-Transit** | HTTPS/TLS 1.2+ | All API and Web traffic encrypted |
| **At-Rest** | SQL Server TDE | Database encryption enabled |
| **Credentials** | Azure Key Vault | No secrets in configuration files |
| **Network** | VNet integration | Isolated from public internet |

### Compliance Readiness

- **GDPR**: Device anonymization options, data retention policies
- **SOC 2**: Audit trails, access controls, encryption
- **ISO 27001**: Security controls, incident logging
- **HIPAA**: Encryption, access logs, audit capabilities

---

## Use Cases & Success Stories

### Scenario 1: UEFI CA 2023 Rollout

**Customer**: Fortune 500 Manufacturing Company  
**Challenge**: Deploy UEFI CA 2023 certificate to 5,000+ devices without boot failures  

**Solution**:
1. Deployed Secure Boot Dashboard to monitor all devices
2. Identified 150 devices ready for immediate update
3. Used batch command feature to deploy certificate
4. Monitored real-time progress via dashboard
5. Identified 200+ devices needing firmware updates
6. Coordinated with hardware vendors for BIOS updates

**Results**:
- ? 100% success rate, zero boot failures
- ? Audit completed in 10 minutes vs. 2 weeks
- ? Proactive firmware maintenance identified
- ? $50,000+ saved in avoided downtime

---

### Scenario 2: Compliance Audit

**Customer**: Global Financial Services  
**Challenge**: Quarterly security audit requires complete certificate inventory  

**Solution**:
1. Exported all 5,000 devices to Excel
2. Filtered for expiring certificates
3. Generated compliance report with color-coding
4. Submitted to auditors with complete certificate details

**Results**:
- ? Audit prep time reduced from 2 weeks to 10 minutes
- ? 100% compliance demonstrated
- ? Audit passed with zero findings
- ? Auditors praised comprehensive reporting

---

### Scenario 3: Firmware Update Prioritization

**Customer**: Healthcare Provider  
**Challenge**: Identify devices with outdated firmware (pre-2024)  

**Solution**:
1. Filtered devices by "Ready to Update = Yellow" (partial readiness)
2. Identified 300+ devices with firmware older than January 2024
3. Prioritized firmware updates by criticality (patient care devices first)
4. Re-evaluated readiness after updates

**Results**:
- ? Proactive firmware maintenance program established
- ? Risk reduced for boot failures
- ? Compliance improved from 60% to 95%
- ? IT efficiency increased (no manual inventory)

---

## Implementation Roadmap

### Phase 1: Pilot Deployment (2-4 weeks)

**Week 1-2: Infrastructure Setup**
- [ ] Provision Azure resources or on-prem servers
- [ ] Deploy API and Web Dashboard
- [ ] Configure Entra ID authentication
- [ ] Set up SQL Server database
- [ ] Apply database migrations

**Week 3-4: Pilot Testing**
- [ ] Deploy client to 10-50 pilot devices
- [ ] Validate certificate enumeration
- [ ] Test command execution
- [ ] Review dashboard metrics
- [ ] Export sample reports

**Deliverables**:
- Working dashboard with pilot devices
- Sample compliance report
- Lessons learned document

---

### Phase 2: Production Rollout (4-8 weeks)

**Week 1-2: Fleet Deployment**
- [ ] Create Intune package (.intunewin)
- [ ] Deploy client to production devices via Intune/SCCM
- [ ] Configure scheduled tasks (hourly or daily)
- [ ] Monitor client version adoption

**Week 3-4: Configuration & Training**
- [ ] Configure alert thresholds
- [ ] Set up email notifications (if enabled)
- [ ] Train administrators on dashboard
- [ ] Train operators on command execution
- [ ] Document runbooks and procedures

**Week 5-6: Certificate Updates**
- [ ] Identify ready devices
- [ ] Plan certificate deployment schedule
- [ ] Execute batch commands
- [ ] Monitor deployment progress
- [ ] Validate successful updates

**Week 7-8: Optimization & Handoff**
- [ ] Review performance metrics
- [ ] Optimize queue processing intervals
- [ ] Fine-tune alert thresholds
- [ ] Document custom configurations
- [ ] Transition to operations team

**Deliverables**:
- Production-ready deployment
- Trained staff
- Complete documentation
- Post-deployment report

---

### Phase 3: Continuous Operation (Ongoing)

**Monthly Activities**:
- [ ] Review compliance dashboard
- [ ] Export monthly reports
- [ ] Check for client updates
- [ ] Monitor certificate expirations
- [ ] Review command history

**Quarterly Activities**:
- [ ] Audit log review
- [ ] Database maintenance
- [ ] Update readiness thresholds
- [ ] Review roadmap and plan upgrades

---

## Pricing & Licensing

### Community Edition (Free)

**Capabilities**:
- Up to 100 devices
- Basic dashboard and reporting
- Certificate enumeration
- Excel/CSV export
- Community support via GitHub

**Ideal For**:
- Small businesses
- Proof of concept
- Evaluation purposes

---

### Professional Edition ($2,500/year)

**Capabilities**:
- Up to 1,000 devices
- **All Community features, plus:**
- Real-time SignalR dashboard
- Remote command execution
- Database-driven settings
- Windows version tracking
- Email support (48h response time)

**Ideal For**:
- Mid-sized enterprises
- Multiple site deployments
- Organizations with dedicated IT teams

---

### Enterprise Edition (Custom Pricing)

**Capabilities**:
- Unlimited devices
- **All Professional features, plus:**
- Premium support with SLA (4h response)
- Custom integrations (SCCM, ServiceNow)
- On-site deployment assistance
- Dedicated support engineer
- Quarterly health checks
- Priority feature requests

**Ideal For**:
- Large enterprises (5,000+ devices)
- Mission-critical deployments
- Regulated industries (finance, healthcare)

**Contact**: sales@securebootdashboard.io

---

## Support & Resources

### Documentation

**User Guides**:
- Getting Started Guide
- Administrator Manual
- Operator Handbook
- Troubleshooting Reference

**API Documentation**:
- REST API Reference (Swagger/OpenAPI)
- SignalR Hub API
- PowerShell Examples
- Integration Guides

**Release Notes**:
- Detailed changelog for each version
- Breaking changes documentation
- Migration guides

**GitHub Repository**:
- https://github.com/robgrame/Nimbus.BootCertWatcher
- Open-source client and shared libraries
- Issue tracker and discussions

---

### Community Support

**GitHub Issues**:
- Bug reports
- Feature requests
- Community Q&A

**GitHub Discussions**:
- Best practices
- Configuration examples
- Integration scenarios

**Response Time**: Best effort (typically 1-3 days)

---

### Professional Support

**Professional Edition** ($2,500/year includes):
- Email support (support@securebootdashboard.io)
- 48-hour response time (business hours)
- Bug fixes and patches
- Quarterly release updates

**Enterprise Edition** (Custom pricing includes):
- Dedicated support engineer
- 4-hour response time (24/7 for P1 issues)
- Phone and email support
- Remote troubleshooting sessions
- On-site visits (if needed)
- Proactive monitoring and health checks

---

### Professional Services

**Deployment Assistance**:
- On-site or remote setup (5-10 days)
- Infrastructure provisioning
- Client deployment via Intune/SCCM
- Configuration and optimization

**Custom Integrations**:
- SCCM integration for automated remediation
- ServiceNow ticket creation
- Power BI report development
- Custom API endpoints

**Training**:
- Administrator training (2-day workshop)
- Operator training (1-day workshop)
- Developer training for customizations
- Train-the-trainer programs

**Health Checks**:
- Quarterly system review
- Performance optimization
- Security assessment
- Upgrade planning

**Pricing**: Contact sales@securebootdashboard.io

---

## Frequently Asked Questions

### General Questions

**Q: What makes Secure Boot Dashboard different from manual certificate checks?**

A: Manual checks require logging into each device, opening PowerShell, running Get-SecureBootUEFI, and documenting results. For 1,000 devices, this could take weeks. Secure Boot Dashboard automates this entirely, providing a centralized dashboard with real-time status for your entire fleet.

---

**Q: Can I use Secure Boot Dashboard with existing SCCM or Intune deployments?**

A: Yes! The client can be deployed via:
- Intune Win32 app
- Intune Proactive Remediation
- SCCM Application or Package
- Group Policy
- Manual installation

The CMTrace logging format integrates seamlessly with Configuration Manager logs.

---

**Q: Does Secure Boot Dashboard modify UEFI firmware?**

A: No. The client is read-only by default. It enumerates certificates and registry settings but does not modify firmware. Certificate updates are performed by Windows Update or explicit administrator commands.

---

### Technical Questions

**Q: What permissions does the client require?**

A: The client requires:
- Local Administrator rights (to read UEFI variables)
- PowerShell execution policy: RemoteSigned or Bypass
- Network access to API endpoint or Azure Queue
- Registry read access for telemetry settings

---

**Q: How much database storage is needed?**

A: Approximately 100 MB per 1,000 devices per month. A typical 5,000-device deployment generates ~500 MB/month. We recommend 100 GB initial database size for growth.

---

**Q: Can I run the dashboard on Linux?**

A: Yes! The API is built on ASP.NET Core 10 and runs on Linux, Windows, or Docker. The client is Windows-only (.NET Framework 4.8) as it reads UEFI firmware.

---

**Q: What happens if the API is offline?**

A: The client has built-in retry logic:
1. Retries failed HTTP POST up to 3 times with exponential backoff
2. Falls back to Azure Queue sink (if configured)
3. Falls back to file share sink (if configured)
4. Caches report locally and retries next execution

No reports are lost due to transient network issues.

---

### Security Questions

**Q: How is sensitive data protected?**

A: Multiple layers:
- **In-transit**: HTTPS/TLS 1.2+ for all traffic
- **At-rest**: SQL Server Transparent Data Encryption (TDE)
- **Authentication**: Entra ID with MFA support
- **Authorization**: Role-based access control (RBAC)
- **Secrets**: Azure Key Vault integration (no secrets in config files)

---

**Q: Does the solution comply with GDPR?**

A: Yes. Device data can be anonymized, and retention policies can be configured. The Device Cleanup feature automatically removes inactive devices after a configurable threshold (e.g., 90 days).

---

**Q: Can I restrict access to specific users?**

A: Yes. The dashboard supports:
- Entra ID authentication with conditional access policies
- Role-based authorization (Admin, Operator, Viewer)
- Azure AD group membership
- Custom roles (Enterprise Edition)

---

### Deployment Questions

**Q: How long does deployment take?**

A: Typical timeline:
- **Pilot** (10-50 devices): 2-4 weeks
- **Production** (1,000 devices): 4-8 weeks
- **Enterprise** (5,000+ devices): 8-12 weeks

Accelerated deployment available with Professional Services.

---

**Q: Do you provide deployment assistance?**

A: Yes:
- **Community Edition**: GitHub documentation and community support
- **Professional Edition**: Email support for deployment questions
- **Enterprise Edition**: Dedicated deployment engineer (on-site or remote)

---

**Q: Can I deploy in an air-gapped network?**

A: Yes. The on-premises deployment option supports:
- No internet connectivity required
- File share sink instead of Azure Queue
- Internal Windows Authentication
- Offline package repository

---

## Next Steps

### 1. Request a Demo

Schedule a personalized demonstration of Secure Boot Dashboard with our team. We'll walk through:
- Dashboard overview and real-time updates
- Certificate tracking and readiness assessment
- Command execution and batch operations
- Reporting and data export

**Contact**: sales@securebootdashboard.io  
**Duration**: 30-45 minutes  
**Format**: Online or on-site

---

### 2. Start a Trial

Download the **Community Edition** (free) and deploy to up to 100 devices:

1. Clone repository: https://github.com/robgrame/Nimbus.BootCertWatcher
2. Follow Quick Start Guide: `docs/DEPLOYMENT_GUIDE.md`
3. Deploy API to Azure or on-premises
4. Install client on pilot devices
5. Access dashboard and explore features

**Trial Duration**: Unlimited (Community Edition is free forever)

---

### 3. Discuss Enterprise Licensing

Contact our sales team to discuss:
- Device count and growth projections
- Deployment timeline and milestones
- Support requirements (SLA, response time)
- Custom integrations and professional services
- Training needs

**Contact**: sales@securebootdashboard.io  
**Phone**: TBD  
**Website**: https://securebootdashboard.io (placeholder)

---

## Appendix A: System Requirements

### Client Requirements

| Component | Requirement |
|-----------|-------------|
| **Operating System** | Windows 10 1607+ or Windows 11 |
| **Firmware** | UEFI-based (not Legacy BIOS) |
| **.NET Framework** | 4.8 (included with Windows) |
| **PowerShell** | 5.1+ (included with Windows) |
| **Execution Policy** | RemoteSigned or Bypass |
| **Permissions** | Local Administrator |
| **Disk Space** | 50 MB + logs (configurable) |
| **Network** | HTTPS outbound to API or Azure Queue |

### API Server Requirements

| Component | Requirement |
|-----------|-------------|
| **Operating System** | Windows Server 2019+ or Linux (Ubuntu 20.04+) |
| **Runtime** | .NET 10 Runtime |
| **Web Server** | IIS 10+ or Kestrel |
| **CPU** | 2 vCPU minimum, 4 vCPU recommended |
| **RAM** | 4 GB minimum, 8 GB recommended |
| **Disk Space** | 20 GB + database storage |
| **Network** | HTTPS inbound (port 443 or 5001) |

### Database Requirements

| Component | Requirement |
|-----------|-------------|
| **Database Engine** | SQL Server 2019+ or Azure SQL Database |
| **Edition** | Express (dev/test), Standard or Enterprise (production) |
| **Compatibility Level** | 150 or higher |
| **Initial Size** | 500 MB |
| **Growth** | ~100 MB per 1,000 devices per month |
| **Backup** | Daily full, hourly transaction log |

### Web Dashboard Requirements

| Component | Requirement |
|-----------|-------------|
| **Browser** | Chrome/Edge 90+, Firefox 88+, Safari 14+ |
| **JavaScript** | Enabled |
| **Cookies** | Enabled (for authentication) |
| **WebSocket** | Supported (for SignalR real-time) |
| **Screen Resolution** | 1280x720 minimum, 1920x1080 recommended |

---

## Appendix B: Glossary

**Certificate Database (db)**: UEFI Secure Boot signature database containing trusted signing certificates

**Certificate Revocation (dbx)**: UEFI forbidden database containing revoked or blacklisted certificates

**Entra ID**: Microsoft Entra ID (formerly Azure Active Directory) for identity and access management

**Key Exchange Key (KEK)**: UEFI database for key exchange keys, typically containing OEM keys

**OEM Certificate**: Original Equipment Manufacturer certificate embedded in device firmware

**Platform Key (PK)**: Root of trust for UEFI Secure Boot, typically set by OEM

**Readiness Assessment**: Multi-criteria evaluation determining if device can safely receive certificate update

**SignalR**: Real-time communication library using WebSocket protocol for live dashboard updates

**UEFI**: Unified Extensible Firmware Interface, modern replacement for BIOS

**UEFI CA 2023**: Microsoft's new UEFI signing certificate replacing expiring 2011 certificate

**UBR**: Update Build Revision, fourth part of Windows version number (e.g., 19045.6456)

---

## Appendix C: References

**Microsoft Documentation**:
- UEFI CA 2023 Certificate Update: https://aka.ms/SecureBootCA
- Secure Boot Overview: https://learn.microsoft.com/windows-hardware/design/device-experiences/oem-secure-boot
- Windows Version History: https://learn.microsoft.com/windows/release-health/

**Industry Standards**:
- UEFI Specification 2.10: https://uefi.org/specifications
- X.509 Certificate Standard: RFC 5280

**Related Tools**:
- Get-SecureBootUEFI PowerShell cmdlet
- Confirm-SecureBootUEFI PowerShell cmdlet
- Windows Hardware Lab Kit (HLK)

---

## Document Information

**Document Version**: 1.0  
**Last Updated**: January 2025  
**Prepared By**: Secure Boot Dashboard Team  
**Contact**: support@securebootdashboard.io  

**Revision History**:
| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2025-01 | Initial release | Development Team |

---

**© 2025 Secure Boot Dashboard. All rights reserved.**

This document is provided "as is" for informational purposes. Features and specifications are subject to change without notice.

**GitHub Repository**: https://github.com/robgrame/Nimbus.BootCertWatcher  
**License**: Open-source components licensed under MIT License

---

**End of Document**
