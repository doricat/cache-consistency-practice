import { makeObservable, observable, action, runInAction, computed } from 'mobx';
import { observer } from 'mobx-react';
import React, { useEffect, useState } from 'react';
import { Navbar, Container, Row, Table, Col, Button, Modal, ListGroup, ListGroupItem } from 'react-bootstrap';

interface ApiResult<T> {
    value: T;
}

interface ValueModel {
    id: number;
    value: string;
}

type ReqStateType = '' | 'waiting' | 'success' | 'failed';

class ReqState {
    constructor() {
        makeObservable(this, {
            state: observable,
            setSuccess: action,
            setfail: action,
            waiting: computed,
            success: computed,
            failed: computed
        });
    }

    state: ReqStateType = '';
    error?: any;

    static waiting(): ReqState {
        return new ReqState();
    }

    static empty(): ReqState {
        return new ReqState();
    }

    setSuccess(): void {
        runInAction(() => {
            this.state = 'success';
        });
    }

    setfail(): void {
        runInAction(() => {
            this.state = 'failed';
        });
    }

    public get waiting(): boolean {
        return this.state === 'waiting';
    }

    public get success(): boolean {
        return this.state === 'success';
    }

    public get failed(): boolean {
        return this.state === 'failed';
    }
}

class Store {
    constructor() {
        makeObservable(this, {
            list: observable,
            getList: action,
            update: action,
            get: action
        });
    }

    list?: ValueModel[];

    getList(): ReqState {
        runInAction(() => {
            this.list = undefined;
        });

        const state = ReqState.waiting();
        fetch('/api/values').then(x => {
            x.json().then(y => {
                runInAction(() => {
                    state.setSuccess();
                    this.list = (y as ApiResult<ValueModel[]>).value;
                });
            });
        }).catch(x => {
            runInAction(() => {
                state.setfail();
            });
        });
        return state;
    }

    update(id: number, model: { value: string, delay: number }): ReqState {
        const state = ReqState.waiting();
        fetch(`/api/values/${id}`, { headers: { 'content-type': 'application/json' }, method: 'PUT', body: JSON.stringify(model) }).then(x => {
            if (x.ok) {
                state.setSuccess();
            }
        }).catch(x => {
            runInAction(() => {
                state.setfail();
            });
        });
        return state;
    }

    get(id: number): ReqState {
        const state = ReqState.waiting();
        fetch(`/api/values/${id}`).then(x => {
            x.json().then(y => {
                runInAction(() => {
                    state.setSuccess();
                    const model = y as ApiResult<ValueModel>;
                    const i = this.list!.findIndex(x => x.id === model.value.id);
                    if (i > -1) {
                        this.list!.splice(i, 1);
                        this.list! = [...this.list!, model.value].sort((x, y) => x.id - y.id);
                    }
                });
            });
        }).catch(x => {
            runInAction(() => {
                state.setfail();
            });
        });
        return state;
    }
}

interface Props {
    store: Store;
}

interface ValueModalProps extends Props {
    show: boolean;
    onHide: () => void;
    model: ValueModel;
}

const ValueModal: React.FC<ValueModalProps> = observer((props: ValueModalProps) => {
    const [updateState, setUpdateState] = useState<ReqState>(ReqState.empty());
    const [getState, setGetState] = useState<ReqState>(ReqState.empty());

    const handleUpdate = (delay: number) => {
        const state = props.store.update(props.model.id, { value: new Date().toISOString(), delay: delay });
        setUpdateState(state);
    };

    const handleRead = () => {
        const state = props.store.get(props.model.id);
        setGetState(state);
    };

    const model = props.store.list!.find(x => x.id === props.model.id)!;

    return (
        <Modal
            size="lg"
            aria-labelledby="contained-modal-title-vcenter"
            backdrop="static"
            show={props.show}
            onHide={props.onHide}
            keyboard={false}
            centered
        >
            <Modal.Header closeButton>
                <Modal.Title id="contained-modal-title-vcenter">模拟更新并读取缓存</Modal.Title>
            </Modal.Header>
            <Modal.Body>
                <ListGroup className="list-group-flush">
                    <ListGroupItem>id: {model.id}</ListGroupItem>
                    <ListGroupItem>value: {model.value}</ListGroupItem>
                </ListGroup>
            </Modal.Body>
            <Modal.Footer>
                <Button variant="secondary" onClick={props.onHide}>
                    关闭
                </Button>
                <Button variant="primary" disabled={updateState.waiting} onClick={() => handleUpdate(0)}>
                    更新随机值，无延迟更新缓存
                </Button>
                <Button variant="primary" disabled={updateState.waiting} onClick={() => handleUpdate(1)}>
                    更新随机值，延迟1s更新缓存
                </Button>
                <Button variant="primary" disabled={updateState.waiting} onClick={() => handleUpdate(10)}>
                    更新随机值，延迟10s更新缓存
                </Button>
                <Button variant="primary" disabled={getState.waiting} onClick={() => handleRead()}>
                    读取最新值
                </Button>
            </Modal.Footer>
        </Modal>
    );
});

const List: React.FC<Props> = observer((props: Props) => {
    const [show, setShow] = useState(false);
    const [current, setCurrent] = useState<ValueModel | undefined>();

    useEffect(() => {
        props.store.getList();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const handleClick = (model: ValueModel) => {
        setCurrent(model);
        setShow(true);
    };

    const handleClose = () => {
        setShow(false);
    };

    if (props.store.list) {
        const elements = props.store.list.map(x => (<tr key={x.value} onDoubleClick={() => handleClick(x)}>
            <td>{x.id}</td>
            <td>{x.value}</td>
        </tr>));

        let modal: JSX.Element | undefined = undefined;
        if (current) {
            modal = (<ValueModal store={props.store} show={show} onHide={handleClose} model={current} />);
        }
        return (
            <>
                <Table striped bordered hover>
                    <thead>
                        <tr>
                            <th>Id</th>
                            <th>Value</th>
                        </tr>
                    </thead>
                    <tbody>
                        {elements}
                    </tbody>
                </Table>
                {modal}
            </>
        );
    }

    return (
        <p>loading...</p>
    );
});

export const App: React.FC = observer(() => {
    const [store] = useState(() => new Store());

    return (
        <>
            <Navbar bg="light" expand="lg">
                <Container>
                    <Navbar.Brand href="/">Practice</Navbar.Brand>
                    <Navbar.Toggle />
                </Container>
            </Navbar>
            <Container>
                <Row>
                    <Col>
                        <List store={store} />
                    </Col>
                </Row>
            </Container>
        </>
    );
});
