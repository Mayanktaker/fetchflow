// © Mayanktaker Computers & Web Development | https://mayanktaker.com
document.addEventListener('DOMContentLoaded', function () {
    window.setTimeout(()=>{
        document.getElementById("link").click();
    },1000);
    document.getElementById("link").href = "fetchflow:chrome-extension://" + chrome.runtime.id + "/";
}, false);