type Props = {
    title: string;
    count?: number;
};

export const View = <T extends Props>(props: T) => <h1 data-count={props.count}>{props.title}</h1>;
