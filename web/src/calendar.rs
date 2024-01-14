use std::rc::Rc;
use std::str::FromStr;

use gloo::events::EventListener;
use icu::calendar::types::IsoWeekday;
use implicit_clone::unsync::IArray;
use wasm_bindgen::prelude::*;
use wasm_bindgen_futures::JsFuture;
use web_sys::{window, Response};
use yew::{prelude::*, suspense::use_future_with};

use crate::{
    about::About,
    event_detail::EventDetail,
    render::{use_rendered, EventOccurrence, MinuteTime, RenderConfig, RenderParams},
    time_slot::TimeSlot,
    Data, Days,
};

#[derive(Eq, PartialEq)]
pub(super) struct SelectedEvent {
    pub time: MinuteTime,
    pub event: Rc<EventOccurrence>,
}

#[derive(Properties, PartialEq)]
pub(super) struct CalendarProps {
    pub data_url: AttrValue,
    pub first_day_of_week: IsoWeekday,
    pub colors: IArray<AttrValue>,
    pub day_names: Days<AttrValue>,
    pub language: Option<AttrValue>,
    pub time_zone: Option<AttrValue>,
}

#[function_component]
pub(super) fn Calendar(props: &CalendarProps) -> HtmlResult {
    let data = use_future_with(props.data_url.clone(), |url| async move {
        let response: Response =
            match JsFuture::from(window().unwrap().fetch_with_str(url.as_str())).await {
                Ok(response) => response.dyn_into().unwrap(),
                Err(error) => wasm_bindgen::throw_val(error),
            };
        if response.status() != 200 {
            wasm_bindgen::throw_str("Server error");
        }
        let data = JsFuture::from(response.text().unwrap())
            .await
            .expect_throw("Network error")
            .as_string()
            .unwrap();
        let parsed: Data = serde_json::from_str(&data).expect_throw("Invalid calendar data");
        (AttrValue::from(data), parsed.meta)
    })?;

    let rendered = use_rendered(RenderParams {
        data: data.0.clone(),
        config: RenderConfig {
            language: props.language.clone(),
            time_zone: props.time_zone.clone(),
        },
    })?
    .clone();

    let mut color_index = vec![
        AttrValue::Static("");
        rendered
            .iter()
            .filter_map(|s| s
                .days
                .as_ref_array::<[_]>(props.first_day_of_week)
                .into_iter()
                .flatten()
                .map(|e| e.id)
                .max())
            .max()
            .map(|v| v + 1)
            .unwrap_or_default() as usize
    ];
    let mut used_colors = 0;
    for slot in rendered.iter() {
        for day in slot.days.as_ref_array::<[_]>(props.first_day_of_week) {
            for event in day {
                let color = &mut color_index[event.id as usize];
                if color == "" {
                    *color = props.colors[used_colors % props.colors.len()].clone();
                    used_colors += 1;
                }
            }
        }
    }
    let color_index = IArray::from(color_index);

    #[derive(Eq, PartialEq)]
    enum Window {
        None,
        Event(SelectedEvent),
        About,
    }

    let selected = use_state_eq(|| Window::None);
    let selected_cb = {
        let selected = selected.clone();
        move |v: SelectedEvent| {
            let hash = format!("{}/{}", v.event.day as u8 % 7, v.event.base_name);
            selected.set(Window::Event(v));
            set_hash(&hash);
        }
    };
    let close = {
        let selected = selected.clone();
        move |_| {
            selected.set(Window::None);
            set_hash("");
        }
    };

    let hash = use_hash();
    if hash.is_empty() {
        selected.set(Window::None);
    } else if &*hash == "about" {
        selected.set(Window::About);
    } else if let Some((day, base_name)) = hash.split_once('/') {
        if let Ok(day) = usize::from_str(day) {
            let day = IsoWeekday::from(day);
            if let Some(occurrence) = rendered.iter().find_map(|s| {
                s.days
                    .as_ref_array::<[_]>(props.first_day_of_week)
                    .into_iter()
                    .flatten()
                    .find(|e| e.day == day && e.base_name == base_name)
                    .map(|e| SelectedEvent {
                        time: s.time,
                        event: e.clone(),
                    })
            }) {
                selected.set(Window::Event(occurrence));
            } else {
                set_hash("");
            }
        }
    }

    let days = props.day_names.as_clone_array(props.first_day_of_week);

    let name = props
        .language
        .as_deref()
        .and_then(|l| data.1.lang.get(l))
        .and_then(|m| m.title.clone())
        .or_else(|| data.1.meta.title.clone());

    use_effect_with(name.clone(), |name| {
        if let (Some(document), Some(name)) = (window().and_then(|w| w.document()), name.as_deref())
        {
            document.set_title(name);
        }
    });

    let show_about = {
        let selected = selected.clone();
        move |_| {
            set_hash("about");
            selected.set(Window::About);
        }
    };

    Ok(html! {
        <>
            <h1><a onclick={show_about}>{name}</a></h1>
            <table>
                <thead>
                    <tr>
                        <th scope="row col"></th>
                        {
                            days.iter().enumerate().map(|(i, d)| html!{
                                <th key={i} scope="col">{d}</th>
                            }).collect::<Html>()
                        }
                    </tr>
                </thead>
                <tbody>
                    {
                        rendered.iter().map(|s| {
                            let time = s.time;
                            html!{<TimeSlot selected={selected_cb.clone()} key={time.0} colors={color_index.clone()} time_slot={s.clone()} first_day_of_week={props.first_day_of_week} />}
                        }).collect::<Html>()
                    }
                </tbody>
            </table>
            {
                match &*selected {
                    Window::Event(selected) => {
                        Some(html! {
                            <EventDetail close={close} start={selected.time} event={selected.event.clone()} />
                        })
                    }
                    Window::About => {
                        Some(html! {
                            <About language={props.language.clone()} metadata={data.1.clone()} close={close} />
                        })
                    }
                    Window::None => {
                        None
                    }
                }
            }
        </>
    })
}

fn set_hash(new_hash: &str) {
    if let Some(window) = window() {
        window.location().set_hash(new_hash).unwrap()
    }
}

#[hook]
fn use_hash() -> UseStateHandle<AttrValue> {
    fn get_hash() -> AttrValue {
        let hash = window().unwrap().location().hash().unwrap();
        let hash = hash.strip_prefix('#').unwrap_or(&hash);
        let hash = js_sys::decode_uri_component(hash)
            .ok()
            .and_then(|s| s.as_string())
            .unwrap_or_default();
        AttrValue::from(hash)
    }
    let hash = use_state_eq(get_hash);
    {
        let hash = hash.clone();
        use_effect(|| {
            let listener = EventListener::new(&window().unwrap(), "hashchange", move |_| {
                hash.set(get_hash());
            });

            move || {
                std::mem::drop(listener);
            }
        });
    }
    hash
}
