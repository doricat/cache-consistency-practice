create table values
(
    id          int         not null primary key,
    value       varchar(50) not null
);

create or replace function notify_update()
    returns trigger
as
$func$
declare
    __json text;
begin
    if (tg_op = 'UPDATE') then
        __json = json_build_object(
                         'id', old.id,
                         'value', new.value,
                         'rowVersion', new.xmin
                     ) #>> '{}';
    end if;

    if (__json is not null) then
        perform pg_notify('update_channel', __json);
    end if;

    return null;
end;
$func$ language plpgsql;

create trigger update_trigger
    after update
    on values
    for each row
execute function notify_update();

insert into values(id, value) values (1, uuid_generate_v4());
insert into values(id, value) values (2, uuid_generate_v4());
insert into values(id, value) values (3, uuid_generate_v4());
insert into values(id, value) values (4, uuid_generate_v4());
insert into values(id, value) values (5, uuid_generate_v4());
insert into values(id, value) values (6, uuid_generate_v4());
insert into values(id, value) values (7, uuid_generate_v4());
insert into values(id, value) values (8, uuid_generate_v4());
insert into values(id, value) values (9, uuid_generate_v4());
insert into values(id, value) values (10, uuid_generate_v4());