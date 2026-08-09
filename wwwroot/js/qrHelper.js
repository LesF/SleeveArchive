let html5QrCode = null;

window.qrHelper = {
    startScanner: function (dotnetRef) {
        if (html5QrCode) {
            html5QrCode.stop().catch(err => console.error(err));
        }

        html5QrCode = new Html5Qrcode("reader");
        const config = { fps: 10, qrbox: { width: 250, height: 250 } };

        html5QrCode.start(
            { facingMode: "environment" },
            config,
            (decodedText) => {
                dotnetRef.invokeMethodAsync("OnQrCodeScanned", decodedText);
                html5QrCode.stop().then(() => {
                    html5QrCode = null;
                }).catch(err => console.error("Error stopping scanner after success:", err));
            },
            (errorMessage) => {
                // Ignore verbose scan attempts
            }
        ).catch(err => {
            console.error("Unable to start scanner:", err);
            dotnetRef.invokeMethodAsync("OnQrCodeScanError", err.message || err.toString());
        });
    },

    stopScanner: function () {
        if (html5QrCode) {
            return html5QrCode.stop().then(() => {
                html5QrCode = null;
            }).catch(err => console.error("Error stopping scanner:", err));
        }
        return Promise.resolve();
    }
};
