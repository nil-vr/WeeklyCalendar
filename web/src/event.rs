use std::rc::Rc;

use yew::prelude::*;

use crate::render;

#[derive(Properties, PartialEq)]
pub(super) struct EventProps {
    pub color: AttrValue,
    pub event: Rc<render::EventOccurrence>,
    pub selected: Callback<Rc<render::EventOccurrence>>,
}

#[function_component]
pub(super) fn Event(props: &EventProps) -> Html {
    let selected = props.selected.clone();
    let e = props.event.clone();
    html! {
        <li>
            <a onclick={Callback::from(move |_| selected.emit(e.clone()))} style={format!("color: {}", props.color)}>{&props.event.name}</a>
        </li>
    }
}
