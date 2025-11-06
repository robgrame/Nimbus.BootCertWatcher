// Splash Screen Management
(function() {
    'use strict';
    
    // Show splash screen on page load
    function showSplashScreen() {
        const splash = document.getElementById('splash-screen');
        if (splash) {
            splash.classList.remove('fade-out');
        }
    }
    
    // Hide splash screen when page is fully loaded
    function hideSplashScreen() {
        const splash = document.getElementById('splash-screen');
        if (splash) {
            // Minimum display time: 1 second
            const minDisplayTime = 1000;
            const loadTime = Date.now() - window.splashStartTime;
            const remainingTime = Math.max(0, minDisplayTime - loadTime);
            
            setTimeout(() => {
                splash.classList.add('fade-out');
                
                // Remove from DOM after animation
                setTimeout(() => {
                    splash.remove();
                }, 500); // Match CSS transition duration
            }, remainingTime);
        }
    }
    
    // Initialize splash screen timer
    window.splashStartTime = Date.now();
    
    // Hide splash when page is fully loaded
    if (document.readyState === 'complete') {
        hideSplashScreen();
    } else {
        window.addEventListener('load', hideSplashScreen);
    }
    
    // Fallback: hide after 5 seconds even if page hasn't finished loading
    setTimeout(hideSplashScreen, 5000);
})();
