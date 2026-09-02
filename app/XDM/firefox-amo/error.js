// © Mayanktaker Computers & Web Development | https://mayanktaker.com
window.onload = function () {
    console.log("error script");
    document.getElementById("OpenLink").addEventListener('click', function () {
        console.log("OpenLink");
        window.open("fetchflow://launch");
        window.close();
    });
};