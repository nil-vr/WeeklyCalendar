use std::rc::Rc;

use icu::calendar::types::IsoWeekday;
use implicit_clone::unsync::IArray;
use yew::prelude::*;

use crate::{calendar::SelectedEvent, event::Event, render};

#[derive(Properties, PartialEq)]
pub(super) struct TimeSlotProps {
    pub time_slot: Rc<render::TimeSlot>,
    pub first_day_of_week: IsoWeekday,
    pub colors: IArray<AttrValue>,
    pub selected: Callback<SelectedEvent>,
}

#[function_component]
pub(super) fn TimeSlot(props: &TimeSlotProps) -> Html {
    let time = props.time_slot.time;
    html! {
        <tr>
            <th scope="row">{props.time_slot.time}</th>
            {
                props.time_slot.days.as_ref_array::<[_]>(props.first_day_of_week).iter().enumerate().map(|(i, d)| {
                    html! {
                        <td key={i}>
                            <ul>
                                {
                                    d.iter().cloned().map(|e| {
                                        let selected = props.selected.clone();
                                        let id = e.id;
                                        html!{ <Event key={id}
                                            color={props.colors[id as usize].clone()}
                                            event={e.clone()}
                                            selected={Callback::from(move |_| selected.emit(SelectedEvent { time, event: e.clone() }))} /> }
                                    }).collect::<Html>()
                                }
                            </ul>
                        </td>
                    }
                }).collect::<Html>()
            }
        </tr>
    }
}
