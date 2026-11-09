// The rest of the work of .NET as a task of its own: the browser may draw a frame before it. A MessageChannel, not setTimeout: the timers of a
// hidden tab run at most once a second, a message is not slowed down
export function yieldToBrowser() {
    return new Promise(resolve => {
        const channel = new MessageChannel();
        channel.port1.onmessage = () => {
            channel.port1.close();
            resolve();
        };
        channel.port2.postMessage(null);
    });
}
