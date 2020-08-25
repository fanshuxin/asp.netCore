// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your Javascript code.
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/opto-hub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

async function start() {
    try {
        await connection.start();
        console.log('SignalR connected');
    } catch (err) {
        console.log(err);
        setTimeout(() => start(), 5000);
        return;
    }
}

start();
