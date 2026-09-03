// © Mayanktaker Computers & Web Development | https://mayanktaker.com
"use strict";
class Logger {
    constructor() {
        this.loggingEnabled = false;
    }

    log(content) {
        if (this.loggingEnabled) {
            console.log(content);
        }
    }
}