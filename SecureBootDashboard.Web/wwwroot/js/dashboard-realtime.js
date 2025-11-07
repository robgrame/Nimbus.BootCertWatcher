/**
 * SecureBootDashboard Real-time Updates using SignalR
 * This module handles SignalR connection and real-time dashboard updates
 */

class DashboardRealtimeClient {
    constructor(hubUrl) {
        this.hubUrl = hubUrl;
        this.connection = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectDelay = 3000; // 3 seconds
        
        // Callbacks
        this.onDeviceUpdated = null;
        this.onNewReportReceived = null;
        this.onComplianceUpdated = null;
        this.onDeviceCountUpdated = null;
        this.onAlertReceived = null;
        this.onConnectionStateChanged = null;
    }

    /**
     * Initialize and start the SignalR connection
     */
    async start() {
        try {
            console.log('[SignalR] Initializing connection to:', this.hubUrl);
            
            // Create connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(this.hubUrl)
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: (retryContext) => {
                        // Exponential backoff: 0s, 2s, 10s, 30s, 60s...
                        if (retryContext.elapsedMilliseconds < 60000) {
                            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 60000);
                        } else {
                            // Stop reconnecting after 1 minute
                            return null;
                        }
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Register event handlers
            this.registerEventHandlers();

            // Connection state handlers
            this.connection.onreconnecting((error) => {
                console.warn('[SignalR] Connection lost. Reconnecting...', error);
                this.isConnected = false;
                this.updateConnectionState('reconnecting');
            });

            this.connection.onreconnected((connectionId) => {
                console.log('[SignalR] Reconnected with ID:', connectionId);
                this.isConnected = true;
                this.reconnectAttempts = 0;
                this.updateConnectionState('connected');
                this.resubscribe();
            });

            this.connection.onclose((error) => {
                console.error('[SignalR] Connection closed', error);
                this.isConnected = false;
                this.updateConnectionState('disconnected');
                
                // Attempt manual reconnect
                if (this.reconnectAttempts < this.maxReconnectAttempts) {
                    setTimeout(() => this.start(), this.reconnectDelay);
                    this.reconnectAttempts++;
                }
            });

            // Start connection
            await this.connection.start();
            console.log('[SignalR] Connected successfully with ID:', this.connection.connectionId);
            this.isConnected = true;
            this.reconnectAttempts = 0;
            this.updateConnectionState('connected');

        } catch (error) {
            console.error('[SignalR] Failed to start connection:', error);
            this.updateConnectionState('error');
            
            // Retry connection
            if (this.reconnectAttempts < this.maxReconnectAttempts) {
                console.log(`[SignalR] Retrying in ${this.reconnectDelay / 1000}s... (Attempt ${this.reconnectAttempts + 1}/${this.maxReconnectAttempts})`);
                this.reconnectAttempts++;
                setTimeout(() => this.start(), this.reconnectDelay);
            }
        }
    }

    /**
     * Register SignalR event handlers
     */
    registerEventHandlers() {
        // Device updated event
        this.connection.on('DeviceUpdated', (data) => {
            console.log('[SignalR] Device updated:', data);
            if (this.onDeviceUpdated) {
                this.onDeviceUpdated(data);
            }
        });

        // New report received event
        this.connection.on('NewReportReceived', (data) => {
            console.log('[SignalR] New report received:', data);
            if (this.onNewReportReceived) {
                this.onNewReportReceived(data);
            }
            
            // Show toast notification
            this.showToastNotification(
                'New Report',
                `New report received from ${data.machineName}`,
                'info'
            );
        });

        // Compliance updated event
        this.connection.on('ComplianceUpdated', (data) => {
            console.log('[SignalR] Compliance updated:', data);
            if (this.onComplianceUpdated) {
                this.onComplianceUpdated(data);
            }
        });

        // Device count updated event
        this.connection.on('DeviceCountUpdated', (data) => {
            console.log('[SignalR] Device count updated:', data);
            if (this.onDeviceCountUpdated) {
                this.onDeviceCountUpdated(data);
            }
        });

        // Alert received event
        this.connection.on('AlertReceived', (data) => {
            console.log('[SignalR] Alert received:', data);
            if (this.onAlertReceived) {
                this.onAlertReceived(data);
            }
            
            // Show alert notification
            this.showToastNotification(
                data.alertType,
                data.message,
                data.severity
            );
        });
    }

    /**
     * Subscribe to dashboard updates
     */
    async subscribeToDashboard() {
        if (!this.isConnected) {
            console.warn('[SignalR] Cannot subscribe: not connected');
            return;
        }

        try {
            await this.connection.invoke('SubscribeToDashboard');
            console.log('[SignalR] Subscribed to dashboard updates');
        } catch (error) {
            console.error('[SignalR] Failed to subscribe to dashboard:', error);
        }
    }

    /**
     * Subscribe to device-specific updates
     */
    async subscribeToDevice(deviceId) {
        if (!this.isConnected) {
            console.warn('[SignalR] Cannot subscribe: not connected');
            return;
        }

        try {
            await this.connection.invoke('SubscribeToDevice', deviceId);
            console.log(`[SignalR] Subscribed to device ${deviceId}`);
        } catch (error) {
            console.error(`[SignalR] Failed to subscribe to device ${deviceId}:`, error);
        }
    }

    /**
     * Unsubscribe from device-specific updates
     */
    async unsubscribeFromDevice(deviceId) {
        if (!this.isConnected) {
            return;
        }

        try {
            await this.connection.invoke('UnsubscribeFromDevice', deviceId);
            console.log(`[SignalR] Unsubscribed from device ${deviceId}`);
        } catch (error) {
            console.error(`[SignalR] Failed to unsubscribe from device ${deviceId}:`, error);
        }
    }

    /**
     * Re-subscribe to all active subscriptions after reconnection
     */
    async resubscribe() {
        console.log('[SignalR] Re-subscribing to active subscriptions...');
        
        // Re-subscribe to dashboard if on homepage
        if (window.location.pathname === '/' || window.location.pathname === '/Index') {
            await this.subscribeToDashboard();
        }
        
        // Re-subscribe to device if on device details page
        const deviceIdMatch = window.location.pathname.match(/\/Devices\/Details\/([a-f0-9-]{36})/i);
        if (deviceIdMatch) {
            await this.subscribeToDevice(deviceIdMatch[1]);
        }
    }

    /**
     * Send ping to test connection
     */
    async ping() {
        if (!this.isConnected) {
            console.warn('[SignalR] Cannot ping: not connected');
            return null;
        }

        try {
            const result = await this.connection.invoke('Ping');
            console.log('[SignalR] Ping response:', result);
            return result;
        } catch (error) {
            console.error('[SignalR] Ping failed:', error);
            return null;
        }
    }

    /**
     * Stop the connection
     */
    async stop() {
        if (this.connection) {
            try {
                await this.connection.stop();
                console.log('[SignalR] Connection stopped');
                this.isConnected = false;
                this.updateConnectionState('disconnected');
            } catch (error) {
                console.error('[SignalR] Error stopping connection:', error);
            }
        }
    }

    /**
     * Update connection state indicator
     */
    updateConnectionState(state) {
        if (this.onConnectionStateChanged) {
            this.onConnectionStateChanged(state);
        }

        // Update OLD small indicator (hidden but keep for compatibility)
        const indicator = document.getElementById('signalr-status-indicator');
        if (indicator) {
            indicator.className = 'signalr-status-indicator';
            
            switch (state) {
                case 'connected':
                    indicator.classList.add('connected');
                    indicator.title = 'Real-time updates: Connected';
                    break;
                case 'reconnecting':
                    indicator.classList.add('reconnecting');
                    indicator.title = 'Real-time updates: Reconnecting...';
                    break;
                case 'disconnected':
                    indicator.classList.add('disconnected');
                    indicator.title = 'Real-time updates: Disconnected';
                    break;
                case 'error':
                    indicator.classList.add('error');
                    indicator.title = 'Real-time updates: Connection error';
                    break;
            }
        }
        
        // Update NEW navbar indicator
        const navbarIndicator = document.getElementById('signalr-status-navbar');
        const navbarIcon = document.getElementById('signalr-icon');
        const navbarText = document.getElementById('signalr-text');
        
        if (navbarIndicator && navbarIcon && navbarText) {
            // Remove all state classes
            navbarIndicator.className = 'nav-link signalr-status-navbar';
            
            switch (state) {
                case 'connected':
                    navbarIndicator.classList.add('connected');
                    navbarIcon.className = 'fas fa-circle-check me-1';
                    navbarText.textContent = 'Real-time Attivo';
                    break;
                case 'reconnecting':
                    navbarIndicator.classList.add('reconnecting');
                    navbarIcon.className = 'fas fa-circle-notch fa-spin me-1';
                    navbarText.textContent = 'Riconnessione...';
                    break;
                case 'disconnected':
                    navbarIndicator.classList.add('disconnected');
                    navbarIcon.className = 'fas fa-circle-xmark me-1';
                    navbarText.textContent = 'Disconnesso';
                    break;
                case 'error':
                    navbarIndicator.classList.add('error');
                    navbarIcon.className = 'fas fa-circle-exclamation me-1';
                    navbarText.textContent = 'Errore';
                    break;
            }
        }
    }

    /**
     * Show toast notification
     */
    showToastNotification(title, message, type = 'info') {
        // Check if Bootstrap toasts are available
        if (typeof bootstrap !== 'undefined' && bootstrap.Toast) {
            // Create toast element
            const toastHTML = `
                <div class="toast align-items-center text-white bg-${this.getBootstrapColor(type)} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="d-flex">
                        <div class="toast-body">
                            <strong>${title}</strong><br>
                            ${message}
                        </div>
                        <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                </div>
            `;
            
            // Find or create toast container
            let toastContainer = document.getElementById('toast-container');
            if (!toastContainer) {
                toastContainer = document.createElement('div');
                toastContainer.id = 'toast-container';
                toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
                toastContainer.style.zIndex = '11';
                document.body.appendChild(toastContainer);
            }
            
            // Add toast to container
            toastContainer.insertAdjacentHTML('beforeend', toastHTML);
            
            // Show toast
            const toastElement = toastContainer.lastElementChild;
            const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 5000 });
            toast.show();
            
            // Remove toast element after it's hidden
            toastElement.addEventListener('hidden.bs.toast', () => {
                toastElement.remove();
            });
        } else {
            // Fallback to console if Bootstrap is not available
            console.log(`[Toast] ${title}: ${message}`);
        }
    }

    /**
     * Map severity/type to Bootstrap color
     */
    getBootstrapColor(type) {
        switch (type.toLowerCase()) {
            case 'success':
                return 'success';
            case 'warning':
                return 'warning';
            case 'error':
            case 'danger':
                return 'danger';
            case 'info':
            default:
                return 'info';
        }
    }
}

// Export for use in other scripts
window.DashboardRealtimeClient = DashboardRealtimeClient;

// Auto-initialize if on supported page
document.addEventListener('DOMContentLoaded', function() {
    // Check if SignalR library is loaded
    if (typeof signalR === 'undefined') {
        console.warn('[SignalR] SignalR library not loaded');
        return;
    }
    
    // Get API base URL from page
    const apiBaseUrl = document.querySelector('meta[name="api-base-url"]')?.content || window.location.origin;
    const hubUrl = `${apiBaseUrl}/dashboardHub`;
    
    // Initialize client
    window.dashboardClient = new DashboardRealtimeClient(hubUrl);
    
    // Start connection
    window.dashboardClient.start();
});
