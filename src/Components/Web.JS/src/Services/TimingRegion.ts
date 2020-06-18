export class TimingRegion {
    static currentRegionsStack: TimingRegion[] = [];

    public children: { [name: string]: TimingRegion } = {};
    public totalDuration: DOMHighResTimeStamp = 0;
    public totalDurationExcludingChildren: DOMHighResTimeStamp | null = 0;
    public totalCount = 0;
    private currentlyActiveStartTime: DOMHighResTimeStamp | null = null;

    public static open(name: string) {
        const region = TimingRegion.currentRegionsStack.length
            ? TimingRegion.getOrCreateChildRegion(name, TimingRegion.currentRegionsStack[TimingRegion.currentRegionsStack.length - 1])
            : new TimingRegion(name); // Root region

        TimingRegion.currentRegionsStack.push(region);

        if (region.currentlyActiveStartTime) {
            throw new Error(`Trying to start timing region ${this.name} when it is already running.`);
        }
        region.currentlyActiveStartTime = performance.now();
        region.totalCount++;

        return region;
    }

    private constructor(private name: string) {
    }

    private static getOrCreateChildRegion(name: string, withinParent: TimingRegion) {
        if (withinParent.children.hasOwnProperty(name)) {
            return withinParent.children[name];
        } else {
            const newRegion = new TimingRegion(name);
            withinParent.children[name] = newRegion;
            return newRegion;
        }
    }

    public close() {
        const endTime = performance.now();
        if (!this.currentlyActiveStartTime) {
            throw new Error(`Trying to stop timing region ${this.name} when it is not running.`);
        }

        const duration = endTime - this.currentlyActiveStartTime;
        this.totalDuration += duration;
        this.currentlyActiveStartTime = null;

        const poppedInstance = TimingRegion.currentRegionsStack.pop();
        if (!poppedInstance) {
            throw new Error(`Timing region disposal mismatch. When trying to pop ${this.name}, stack was empty.`);
        } else if (poppedInstance !== this) {
            throw new Error(`Timing region disposal mismatch. When trying to pop ${this.name}, actually found ${poppedInstance.name}`);
        }
    }

    public logAll() {
        console.log('Top down:');
        this.logTopDown();

        console.log('Flattened:');
        this.logFlattened();
    }

    public logTopDown() {
        function logTopDownEntry(logType: 'group' | 'log', name: string, count: number | null, duration: number) {
            console[logType].call(console,
                `%c${name}%c Count: %c${count || '-'}%c Duration: %c${duration.toFixed(2)}`,
                'background: black; color: white; padding: 0 3px;',
                '',
                'color: red;',
                '',
                'color: blue');
        }

        logTopDownEntry('group', this.name, this.totalCount, this.totalDuration);

        let durationAccounted = 0;
        const sortedChildren = Object.values(this.children).sort((a, b) => b.totalDuration - a.totalDuration);
        sortedChildren.forEach(child => {
            durationAccounted += child.totalDuration;
            child.logTopDown();
        });

        this.totalDurationExcludingChildren = this.totalDuration - durationAccounted;
        logTopDownEntry('log', '(Unaccounted)', null, this.totalDurationExcludingChildren);

        console.groupEnd();
    }

    public logFlattened() {
        const groups: { [name: string]: { count: number; duration: DOMHighResTimeStamp, durationExcludingChildren: DOMHighResTimeStamp } } = {};
        processRecursive(this);
        console.table(groups);

        function processRecursive(region: TimingRegion) {
            if (!groups.hasOwnProperty(region.name)) {
                groups[region.name] = { count: 0, duration: 0, durationExcludingChildren: 0 };
            }

            groups[region.name].count += region.totalCount;
            groups[region.name].duration += region.totalDuration;

            if (region.totalDurationExcludingChildren === null) {
                throw new Error(`Group ${region.name} has no totalDurationExcludingChildren, but should do.`);
            }
            groups[region.name].durationExcludingChildren += region.totalDurationExcludingChildren;

            Object.values(region.children).forEach(processRecursive);
        }
    }
}
